using System.Text.Json;
using ResetExplode.Hardware;

internal static class StabilitySessionChecks
{
    private const string Boot = "00112233445566778899aabbccddeeff";
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static StabilitySample Sample(ulong seconds, params EdacController[] controllers) =>
        new(Boot, seconds * 1_000_000, seconds * 1_000_000 + 1000, new(DateTimeOffset.UnixEpoch.AddSeconds(seconds), controllers));
    private static KernelLogSnapshot Logs(params KernelLogEntry[] entries) => new(DateTimeOffset.UtcNow, entries, "Amostra do journal.");
    private static KernelLogEntry Entry(ulong? micros, string? boot = Boot) =>
        new(DateTimeOffset.UnixEpoch, 4, "mce: sample") { BootId = boot, MonotonicMicroseconds = micros };

    public static void Run()
    {
        var baseline = Sample(100, new EdacController("mc0", "Controller", 10, 2, 100));
        var current = Sample(110, new EdacController("mc0", "Controller", 13, 3, 110));
        var result = StabilitySession.Compare(baseline, current, Logs(Entry(100_001_001), Entry(110_000_000),
            Entry(100_001_000), Entry(110_000_001), Entry(105_000_000, "different"), Entry(null)));
        Check(result.Controllers[0].Corrected == 3 && result.Controllers[0].Uncorrected == 1, "Delta incorreto.");
        Check(result.Logs.Count == 2 && result.Status.Contains("1 registros sem"), "Intervalo/inicialização/ausência incorretos.");
        Check(result.Description.Contains("CPU/memória 2"), "Classificação do intervalo perdida.");
        var backdated = current with { Edac = current.Edac with { Timestamp = DateTimeOffset.UnixEpoch } };
        Check(StabilitySession.Compare(baseline, backdated, Logs()).Controllers[0].Corrected == 3, "Relógio civil afetou delta.");
        foreach (var bad in new[] {
            new EdacController("mc0", "Controller", 9, 3, 110),
            new EdacController("mc0", "Controller", 13, 1, 110),
            new EdacController("mc0", "Controller", 13, 3, 1),
            new EdacController("mc0", "Controller", 13, 3, 105), // reset que superou valor anterior
            new EdacController("mc0", "Controller", 13, 3, 130),
            new EdacController("mc0", "Changed", 13, 3, 110),
            new EdacController("mc0", "Controller", null, 3, 110),
            new EdacController("mc0", "Controller", 13, 3, null) })
        {
            var invalid = StabilitySession.Compare(baseline, Sample(110, bad), Logs());
            Check(invalid.Controllers[0].Corrected == null && invalid.Controllers[0].Uncorrected == null, "Descontinuidade virou delta.");
        }
        Check(StabilitySession.Compare(baseline, Sample(110), Logs()).Controllers[0].Corrected == null, "Remoção virou zero.");
        Check(StabilitySession.Compare(Sample(100), current, Logs()).Controllers[0].Corrected == null, "Controlador novo virou delta.");
        Check(StabilitySession.Compare(baseline, Sample(110, current.Edac.Controllers[0], current.Edac.Controllers[0]), Logs()).Controllers[0].Corrected == null, "Identidade duplicada aceita.");
        Check(StabilitySession.Compare(baseline, current with { BootId = null }, Logs()).Status.Contains("indisponível"), "Boot ausente aceito.");
        Check(StabilitySession.Compare(baseline, current with { BootId = "other" }, Logs(Entry(105_000_000))).Logs.Count == 0, "Reboot aceito.");
        Check(StabilitySession.Compare(current, baseline, Logs()).Status.Contains("inválido"), "Intervalo invertido aceito.");
        var max = Sample(110, new EdacController("mc0", "Controller", ulong.MaxValue, ulong.MaxValue, 110));
        Check(StabilitySession.Compare(baseline, max, Logs()).Controllers[0].Corrected == ulong.MaxValue - 10, "Overflow de delta.");
        var zero = Sample(110, new EdacController("mc0", "Controller", 10, 2, 110));
        Check(StabilitySession.Compare(baseline, zero, Logs()).Controllers[0].Corrected == 0, "Zero válido perdeu semântica.");
        var unavailable = StabilitySession.Compare(baseline, current, new(DateTimeOffset.UtcNow, [], "Logs indisponíveis."));
        Check(unavailable.Controllers[0].Corrected == 3 && unavailable.Status.Contains("Logs indisponíveis"), "Falha de logs escondeu EDAC/indisponibilidade.");
        // Leitor preserva metadados opcionais e normaliza boot ID, sem descartar mensagens antigas sem monotônico.
        var fields = new Dictionary<string, string> { ["_BOOT_ID"] = "00112233-4455-6677-8899-aabbccddeeff", ["_TRANSPORT"] = "kernel",
            ["PRIORITY"] = "4", ["MESSAGE"] = "test", ["__REALTIME_TIMESTAMP"] = "1000", ["__MONOTONIC_TIMESTAMP"] = "100001001" };
        var parsed = LinuxKernelLogReader.Parse(JsonSerializer.Serialize(fields), 0, false);
        Check(parsed.Entries[0].BootId == Boot && parsed.Entries[0].MonotonicMicroseconds == 100001001, "Metadados não preservados.");
        fields["__MONOTONIC_TIMESTAMP"] = "-1";
        Check(LinuxKernelLogReader.Parse(JsonSerializer.Serialize(fields), 0, false).Entries[0].MonotonicMicroseconds == null, "Tempo inválido virou zero.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { StabilitySession.Capture(cancelled.Token); throw new InvalidOperationException("Cancelamento ignorado."); }
        catch (OperationCanceledException) { }
        Console.WriteLine("OK: sessão de estabilidade, deltas, resets, hotplug, reboot, relógios, limites e metadados do journal.");
    }
}
