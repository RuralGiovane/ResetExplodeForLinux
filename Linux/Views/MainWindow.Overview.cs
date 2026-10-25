using ResetExplode.Hardware;

namespace ResetExplode;

public partial class MainWindow
{
    private void ApplySystemOverview(LinuxSystemInfo system)
    {
        OverviewMachineText.Text = Environment.MachineName;
        var labels = new (string Source, string Label)[]
        {
            ("Distribuição", "Sistema"),
            ("Kernel", "Kernel"),
            ("Arquitetura do processo", "Arquitetura¹"),
            ("Processador", "Processador"),
            ("Threads disponíveis ao processo", "Threads¹"),
            ("Placa-mãe", "Placa-mãe"),
            ("BIOS", "BIOS"),
            ("RAM total do sistema", "Memória total")
        };
        OverviewEntries.ItemsSource = labels.Select(item => new SystemEntry(item.Label,
            system.Entries.FirstOrDefault(e => e.Name == item.Source)?.Value ?? "Indisponível")).ToArray();
    }

    private void ApplyGpuOverview(TelemetrySnapshot snapshot)
    {
        var sources = snapshot.Sensors.Where(s => s.Component == HardwareComponent.Gpu)
            .Select(s => s.Hardware).Distinct(StringComparer.Ordinal).ToArray();
        string value = sources.Length == 0 ? "Identificação indisponível na telemetria atual" : string.Join("\n", sources);
        // Evitar recriar o texto/layout a cada leitura quando a identificação não mudou.
        if (OverviewGpuText.Text != value) OverviewGpuText.Text = value;
    }

    private void ApplyStorageOverview()
    {
        var roots = _mountInventory?.Mounts.Where(m => m.MountPoint == "/").ToArray();
        OverviewDiskText.Text = roots is { Length: 1 } && roots[0].Space is { } space
            ? $"{space.UsedBytes / 1073741824d:N1} / {space.TotalBytes / 1073741824d:N1} GiB usados · {roots[0].FileSystem}"
            : "Capacidade indisponível";
    }
}
