using System.Runtime.InteropServices;

namespace ResetExplode.Hardware;

public sealed record StabilitySample(string? BootId, ulong StartMicroseconds, ulong EndMicroseconds, EdacSnapshot Edac);
public sealed record EdacDifference(string Id, ulong? Corrected, ulong? Uncorrected, string Status);
public sealed record StabilityComparison(IReadOnlyList<EdacDifference> Controllers, IReadOnlyList<KernelLogEntry> Logs, string Status)
{
    public string Description => Status + "\n\nEDAC · diferenças desde a referência\n" +
        (Controllers.Count == 0 ? "Nenhum controlador disponível para comparar." : string.Join("\n", Controllers.Select(c =>
            $"{c.Id} · CE: {c.Corrected?.ToString() ?? "Indisponível"} · UE: {c.Uncorrected?.ToString() ?? "Indisponível"} · {c.Status}"))) +
        "\n\nLOGS · recebidos no intervalo\n" + KernelLogClassifier.Summarize(Logs) + "\n\n" +
        string.Join("\n\n", Logs.Select(e => e.Description));
}

public static class StabilitySession
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec { public long Seconds; public long Nanoseconds; }
    [DllImport("libc", SetLastError = true)]
    private static extern int clock_gettime(int clock, out Timespec time);

    private static ulong MonotonicMicroseconds()
    {
        if (!OperatingSystem.IsLinux() || IntPtr.Size != 8 || clock_gettime(1, out var time) != 0 || time.Seconds < 0)
            throw new NotSupportedException("Relógio monotônico Linux de 64 bits indisponível.");
        return checked((ulong)time.Seconds * 1_000_000 + (ulong)time.Nanoseconds / 1000);
    }

    public static StabilitySample Capture(CancellationToken cancellation = default)
    {
        var files = new LinuxFiles();
        cancellation.ThrowIfCancellationRequested();
        string? boot = NormalizeBootId(files.Read("/proc/sys/kernel/random/boot_id"));
        ulong start = MonotonicMicroseconds();
        var edac = new LinuxEdacCollector(files).Collect(cancellation);
        ulong end = MonotonicMicroseconds();
        if (boot != NormalizeBootId(files.Read("/proc/sys/kernel/random/boot_id"))) boot = null;
        return new(boot, start, end, edac);
    }

    internal static string? NormalizeBootId(string? value) => Guid.TryParse(value, out var id) ? id.ToString("N") : null;

    public static StabilityComparison Compare(StabilitySample baseline, StabilitySample current, KernelLogSnapshot logs)
    {
        if (baseline.BootId == null || current.BootId == null || baseline.BootId != current.BootId)
            return new([], [], "Comparação indisponível: inicialização diferente ou identificação ausente. Crie uma nova referência.");
        if (baseline.EndMicroseconds < baseline.StartMicroseconds || current.EndMicroseconds < current.StartMicroseconds ||
            current.StartMicroseconds < baseline.EndMicroseconds)
            return new([], [], "Comparação indisponível: intervalo monotônico inválido. Crie uma nova referência.");
        var differences = new List<EdacDifference>();
        foreach (var id in baseline.Edac.Controllers.Select(c => c.Id).Union(current.Edac.Controllers.Select(c => c.Id)).Order(StringComparer.Ordinal))
        {
            var previous = baseline.Edac.Controllers.Where(c => c.Id == id).ToArray();
            var latest = current.Edac.Controllers.Where(c => c.Id == id).ToArray();
            string? problem = previous.Length != 1 || latest.Length != 1 ? "Controlador adicionado, removido ou identificação ambígua." : null;
            if (problem == null)
            {
                var a = previous[0]; var b = latest[0];
                if (a.Name != b.Name || string.IsNullOrWhiteSpace(a.Name) || a.Name == "Nome indisponível")
                    problem = "Identificação alterada ou indisponível.";
                else if (!a.Complete || !b.Complete || a.SecondsSinceReset == null || b.SecondsSinceReset == null)
                    problem = "Contadores ou período de reset indisponíveis.";
                else if (b.Corrected < a.Corrected || b.Uncorrected < a.Uncorrected || b.SecondsSinceReset < a.SecondsSinceReset)
                    problem = "Contadores recuaram: possível reset ou troca do controlador.";
                else
                {
                    decimal elapsedMin = (current.StartMicroseconds - baseline.EndMicroseconds) / 1_000_000m;
                    decimal elapsedMax = (current.EndMicroseconds - baseline.StartMicroseconds) / 1_000_000m;
                    decimal counterElapsed = b.SecondsSinceReset.Value - a.SecondsSinceReset.Value;
                    if (counterElapsed < elapsedMin - 2 || counterElapsed > elapsedMax + 2)
                        problem = "Período inconsistente: possível reset ou descontinuidade da leitura.";
                    else differences.Add(new(id, b.Corrected!.Value - a.Corrected!.Value,
                        b.Uncorrected!.Value - a.Uncorrected!.Value, "Diferença observada; continuidade não garantida."));
                }
            }
            if (problem != null) differences.Add(new(id, null, null, problem));
        }
        var interval = logs.Entries.Where(e => e.BootId == baseline.BootId && e.MonotonicMicroseconds is { } time &&
            time > baseline.EndMicroseconds && time <= current.StartMicroseconds).ToArray();
        int unknown = logs.Entries.Count(e => e.BootId == null || e.MonotonicMicroseconds == null);
        string status = $"Referência: {baseline.Edac.Timestamp.ToLocalTime():dd/MM/yyyy HH:mm:ss} · Leitura: {current.Edac.Timestamp.ToLocalTime():dd/MM/yyyy HH:mm:ss}. " +
            "Comparação manual, sem atribuir ocorrências a um benchmark. Reset rápido ou substituição com a mesma identidade podem não ser detectados.\n" +
            "Logs filtrados pelo horário monotônico de recebimento no journal, entre o fim da referência e o início da leitura atual. " +
            "Amostra limitada aos últimos 500 avisos/erros; não garante cobertura integral do período.\n" + logs.Status;
        if (unknown > 0) status += $" {unknown} registros sem identificação/tempo monotônico foram excluídos do intervalo.";
        return new(differences.ToArray(), interval, status);
    }
}
