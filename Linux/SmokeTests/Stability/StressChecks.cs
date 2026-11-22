using System.Diagnostics;
using ResetExplode;

internal static class StressChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
        }
        static void Reject(Action action)
        {
            try { action(); }
            catch (ArgumentException) { return; }
            catch (InvalidOperationException) { return; }
            throw new Exception("Configuração inválida aceita pelo stress.");
        }
        StressTest.ValidateMemory(16 * StressTest.MiB, 64 * StressTest.MiB, 64 * StressTest.MiB);
        Reject(() => StressTest.ValidateMemory(16 * StressTest.MiB, null, long.MaxValue));
        Reject(() => StressTest.ValidateMemory(16 * StressTest.MiB, 63 * StressTest.MiB, long.MaxValue));
        Reject(() => StressTest.ValidateMemory(16 * StressTest.MiB, long.MaxValue, 63 * StressTest.MiB));
        Reject(() => StressTest.Run(false, 0, TimeSpan.FromSeconds(1), null, default, 1));
        Reject(() => StressTest.Run(true, 0, TimeSpan.Zero, null, default, 1));
        Reject(() => StressTest.Run(true, 0, TimeSpan.FromMinutes(31), null, default, 1));

        // Uma cauda após o bloco de cancelamento precisa ser escrita e verificada.
        var data = new long[8195];
        Check(StressTest.WritePattern(data, 123, () => true), "Escrita incompleta.");
        Check(StressTest.VerifyPattern(data, 123, () => true), "Verificação incompleta.");
        data[^1] ^= 1;
        Reject(() => StressTest.VerifyPattern(data, 123, () => true));
        Check(!StressTest.WritePattern(data, 0, () => false), "Escrita ignorou interrupção.");
        Check(!StressTest.VerifyPattern(data, 0, () => false), "Verificação ignorou interrupção.");

        foreach (bool cpu in new[] { false, true })
        {
            using var cancel = new CancellationTokenSource();
            int reports = 0;
            try
            {
                await Task.Run(() => StressTest.Run(cpu, StressTest.MiB, TimeSpan.FromSeconds(20),
                    _ => { reports++; cancel.Cancel(); }, cancel.Token, 2)).WaitAsync(TimeSpan.FromSeconds(5));
                throw new Exception("Stress cancelado retornou resultado completo.");
            }
            catch (OperationCanceledException) { }
            Check(reports > 0, "Carga não publicou progresso.");
        }
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try
            {
                StressTest.Run(true, 0, TimeSpan.FromSeconds(1), _ => throw new Exception("Iniciou após cancelar"), cancelled.Token, 1);
                throw new Exception("Pré-cancelamento ignorado.");
            }
            catch (OperationCanceledException) { }
        }

        var result = await Task.Run(() => StressTest.Run(true, StressTest.MiB, TimeSpan.FromMilliseconds(500),
            null, default, 2)).WaitAsync(TimeSpan.FromSeconds(5));
        Check(result.CpuThreads == 1 && result.AllocatedBytes == StressTest.MiB && result.MemoryPasses > 0,
            "Carga combinada não verificou RAM/reservou worker.");
        Check(result.Seconds >= .5, "Carga terminou antes do prazo.");
        var cpuOnly = StressTest.Run(true, 0, TimeSpan.FromMilliseconds(20), null, default, 1);
        Check(cpuOnly.CpuThreads == 1 && cpuOnly.AllocatedBytes == 0 && cpuOnly.MemoryPasses == 0,
            "CPU isolada alocou RAM de teste.");

        // Falhas do observador encerram a carga e permitem uma nova execução.
        try
        {
            await Task.Run(() => StressTest.Run(true, StressTest.MiB, TimeSpan.FromSeconds(20),
                _ => throw new IOException("Observador indisponível"), default, 2)).WaitAsync(TimeSpan.FromSeconds(5));
            throw new Exception("Falha do observador ignorada.");
        }
        catch (IOException) { }

        var runner = new BenchmarkRunner();
        bool started = false;
        try
        {
            await runner.RunThermalAsync(ct => { started = true; return StressTest.Run(true, 0, TimeSpan.FromSeconds(1), null, ct, 1); },
                default, new CpuThermalPolicy(90), () => null);
            throw new Exception("Carga sem temperatura iniciada.");
        }
        catch (ThermalStopException) { }
        Check(!started && !runner.IsRunning, "Pré-checagem térmica falhou.");
        bool heated = false;
        try
        {
            await runner.RunThermalAsync(ct => StressTest.Run(true, StressTest.MiB, TimeSpan.FromSeconds(20),
                    _ => Volatile.Write(ref heated, true), ct, 2), default, new CpuThermalPolicy(90),
                () => new(Volatile.Read(ref heated) ? 95 : 40, Stopwatch.GetTimestamp())).WaitAsync(TimeSpan.FromSeconds(5));
            throw new Exception("Limite térmico ignorado.");
        }
        catch (ThermalStopException) { }
        Check(!runner.IsRunning, "Workers térmicos não liberaram o coordenador.");
        Check(await runner.RunAsync(_ => 7, default) == 7, "Coordenador não aceitou nova execução.");
        Console.WriteLine("OK: stress CPU/RAM, orçamento, padrões/caudas/corrupção, prazo, cancelamento, falha do observador e proteção térmica.");
    }
}
