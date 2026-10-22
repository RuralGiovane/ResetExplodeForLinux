namespace ResetExplode;

/// <summary>Coordena execução fora da UI e impede testes simultâneos ou resultados de testes cancelados.</summary>
public sealed class BenchmarkRunner
{
    private int _running;
    public bool IsRunning => Volatile.Read(ref _running) != 0;

    public async Task<T> RunThermalAsync<T>(Func<CancellationToken, T> benchmark, CancellationToken cancellation,
        CpuThermalPolicy policy, Func<CpuTemperatureReading?> readTemperature)
    {
        cancellation.ThrowIfCancellationRequested();
        using var monitor = new CpuThermalMonitor(policy, readTemperature, cancellation);
        try
        {
            monitor.ThrowIfStopped(); // Não iniciar carga sem uma leitura válida abaixo do limite.
            T result = await RunAsync(ct =>
            {
                monitor.Check(); // Verificar novamente após eventual espera na fila do pool.
                monitor.ThrowIfStopped();
                ct.ThrowIfCancellationRequested();
                return benchmark(ct);
            }, monitor.Token).ConfigureAwait(false);
            monitor.Check(); // Não publicar resultado se a leitura se perdeu no fim do teste.
            monitor.ThrowIfStopped();
            cancellation.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException) when (monitor.Stop != null)
        {
            throw new ThermalStopException(monitor.Stop!);
        }
    }

    public async Task<T> RunAsync<T>(Func<CancellationToken, T> benchmark, CancellationToken cancellation)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            throw new InvalidOperationException("Já existe um benchmark em execução.");
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var result = await Task.Run(() => benchmark(cancellation), cancellation).ConfigureAwait(false);
            // Os algoritmos originais podem retornar uma medição parcial ao cancelar.
            cancellation.ThrowIfCancellationRequested();
            return result;
        }
        finally { Volatile.Write(ref _running, 0); }
    }
}
