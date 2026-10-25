using System.Globalization;

namespace ResetExplode.Hardware;

public sealed record EdacController(string Id, string Name, ulong? Corrected, ulong? Uncorrected, ulong? SecondsSinceReset)
{
    public bool Complete => Corrected.HasValue && Uncorrected.HasValue;
    public string Description => $"{Id} · {Name}\nCorrigidos (CE): {Display(Corrected)} · Não corrigidos (UE): {Display(Uncorrected)}\nSegundos desde reset dos contadores: {Display(SecondsSinceReset)}";
    private static string Display(ulong? number) => number?.ToString(CultureInfo.CurrentCulture) ?? "Indisponível";
}
public sealed record EdacSnapshot(DateTimeOffset Timestamp, IReadOnlyList<EdacController> Controllers)
{
    public string Status => Controllers.Count == 0 ? "EDAC indisponível: nenhum controlador acessível foi encontrado."
        : Controllers.Any(c => c.Corrected > 0 || c.Uncorrected > 0)
            ? "Há erros reportados nos contadores EDAC; consulte os valores e o período disponível."
            : Controllers.Any(c => !c.Complete) ? "Leitura incompleta: um ou mais contadores estão indisponíveis."
            : "Zero erros reportados pelos controladores acessíveis. Isso não certifica estabilidade.";
}

/// <summary>Contadores cumulativos de controladores, sem somar novamente DIMMs/canais e sem reset.</summary>
public sealed class LinuxEdacCollector(LinuxFiles files)
{
    public EdacSnapshot Collect(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var controllers = new List<EdacController>();
        foreach (string path in files.Directories("/sys/devices/system/edac/mc", "mc*"))
        {
            cancellation.ThrowIfCancellationRequested();
            string id = Path.GetFileName(path);
            if (id.Length <= 2 || !id.AsSpan(2).ToArray().All(char.IsAsciiDigit)) continue;
            ulong? Read(string name) => ulong.TryParse(files.Read(path + "/" + name), NumberStyles.None,
                CultureInfo.InvariantCulture, out ulong value) ? value : null;
            controllers.Add(new(id, files.Read(path + "/mc_name") ?? "Nome indisponível",
                Read("ce_count"), Read("ue_count"), Read("seconds_since_reset")));
        }
        cancellation.ThrowIfCancellationRequested();
        return new(DateTimeOffset.UtcNow, controllers.ToArray());
    }
}
