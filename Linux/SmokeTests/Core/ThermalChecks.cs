using System.Diagnostics;
using ResetExplode;

internal static class ThermalChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static async Task ExpectStop(Func<Task> action, ThermalStopReason reason)
    {
        try { await action(); }
        catch (ThermalStopException ex)
        {
            Check(ex.Stop.Reason == reason, $"Motivo térmico incorreto: {ex.Stop.Reason}, esperado {reason}.");
            return;
        }
        throw new InvalidOperationException("O teste deveria ter sido bloqueado/interrompido sem resultado.");
    }

    public static async Task RunAsync()
    {
        var policy = new CpuThermalPolicy(90);
        Check(policy.Evaluate(89.9, TimeSpan.Zero) == null, "Leitura abaixo do limite foi rejeitada.");
        Check(policy.Evaluate(90, TimeSpan.Zero)?.Reason == ThermalStopReason.LimitReached, "Igualdade com limite não interrompeu.");
        Check(policy.Evaluate(40, TimeSpan.FromSeconds(5))?.Reason == ThermalStopReason.StaleReading, "Leitura de 5 s foi aceita.");
        Check(policy.Evaluate(40, TimeSpan.FromSeconds(-1))?.Reason == ThermalStopReason.StaleReading, "Timestamp futuro foi aceito.");
        foreach (double? value in new double?[] { null, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Check(policy.Evaluate(value, TimeSpan.Zero)?.Reason == ThermalStopReason.TemperatureUnavailable, "Temperatura inválida foi aceita.");
        foreach (double value in new[] { 59.0, 101.0, double.NaN })
        {
            bool rejected = false;
            try { _ = new CpuThermalPolicy(value); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Limite inválido foi aceito.");
        }

        var runner = new BenchmarkRunner();
        long now = Stopwatch.GetTimestamp();
        var preflight = new (CpuTemperatureReading? Reading, ThermalStopReason Reason)[]
        {
            (null, ThermalStopReason.TemperatureUnavailable),
            (new(null, now), ThermalStopReason.TemperatureUnavailable),
            (new(91, now), ThermalStopReason.LimitReached),
            (new(40, now - 6 * Stopwatch.Frequency), ThermalStopReason.StaleReading)
        };
        foreach (var scenario in preflight)
        {
            bool started = false;
            await ExpectStop(() => runner.RunThermalAsync(_ => { started = true; return 123; },
                CancellationToken.None, policy, () => scenario.Reading), scenario.Reason);
            Check(!started && !runner.IsRunning, "Pré-checagem térmica deixou o trabalho iniciar.");
        }

        // O próprio trabalho simula perda/alteração do sensor; sem aquecer CPU ou bloquear a UI.
        foreach (ThermalStopReason reason in Enum.GetValues<ThermalStopReason>())
        {
            int phase = 0;
            CpuTemperatureReading? Read()
            {
                long timestamp = Stopwatch.GetTimestamp();
                if (Volatile.Read(ref phase) == 0) return new(40, timestamp);
                return reason switch
                {
                    ThermalStopReason.LimitReached => new(95, timestamp),
                    ThermalStopReason.StaleReading => new(40, timestamp - 6 * Stopwatch.Frequency),
                    _ => null
                };
            }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ExpectStop(() => runner.RunThermalAsync(ct =>
            {
                Volatile.Write(ref phase, 1);
                Check(ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)), "O watchdog não cancelou a execução.");
                return 123; // Simula algoritmo que retorna resultado parcial ao observar cancelamento.
            }, timeout.Token, policy, Read).WaitAsync(TimeSpan.FromSeconds(8)), reason);
            Check(!runner.IsRunning, "Interrupção térmica deixou o coordenador ocupado.");
        }

        int completed = 0;
        await ExpectStop(() => runner.RunThermalAsync(_ => { Volatile.Write(ref completed, 1); return 7; },
            CancellationToken.None, policy, () => new(Volatile.Read(ref completed) == 0 ? 40 : 95, Stopwatch.GetTimestamp())),
            ThermalStopReason.LimitReached);

        // Cancelamento manual continua distinguível do cancelamento por temperatura.
        using (var manual = new CancellationTokenSource())
        {
            bool cancelled = false;
            try
            {
                await runner.RunThermalAsync(_ => { manual.Cancel(); return 7; }, manual.Token,
                    policy, () => new(40, Stopwatch.GetTimestamp()));
            }
            catch (OperationCanceledException ex)
            {
                Check(ex is not ThermalStopException, "Cancelamento manual foi classificado como térmico.");
                cancelled = true;
            }
            Check(cancelled, "Cancelamento manual foi ignorado.");
        }

        // Após descartar o monitor, seu watchdog não deve continuar consultando a fonte.
        int reads = 0;
        int result = await runner.RunThermalAsync(_ => 7, CancellationToken.None, policy, () =>
        {
            Interlocked.Increment(ref reads);
            return new(40, Stopwatch.GetTimestamp());
        });
        int after = Volatile.Read(ref reads);
        await Task.Delay(350);
        Check(result == 7 && Volatile.Read(ref reads) == after && !runner.IsRunning,
            "Resultado válido falhou ou o monitor continuou rodando após o término.");
        Console.WriteLine("OK: limite térmico, pré-checagem, perda/atraso de leitura, cancelamento e descarte parcial.");
    }
}
