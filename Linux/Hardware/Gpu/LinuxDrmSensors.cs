using System.Text.RegularExpressions;

namespace ResetExplode.Hardware;

/// <summary>Read-only DRM metrics. Does not infer temperature or utilization from clocks.</summary>
public sealed class LinuxDrmSensors(LinuxFiles files)
{
    public GpuSnapshot Read(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var rows = new List<SensorReading>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string entry in files.Directories("/sys/class/drm", "card*"))
        {
            cancellation.ThrowIfCancellationRequested();
            if (!Regex.IsMatch(Path.GetFileName(entry), @"\Acard[0-9]+\z")) continue;
            // Resolve the class symlink first: device's relative target is based
            // on the physical card directory, not on /sys/class/drm.
            string? card = files.DirectoryTarget(entry);
            if (card == null) continue;
            string? device = files.DirectoryTarget(card + "/device");
            if (device == null) continue;
            string pci = Path.GetFileName(device);
            if (!Regex.IsMatch(pci, @"\A[0-9a-f]{4}:[0-9a-f]{2}:[0-1][0-9a-f]\.[0-7]\z")) continue;
            string? driverPath = files.DirectoryTarget(device + "/driver");
            string? driver = driverPath == null ? null : Path.GetFileName(driverPath);
            string? vendor = files.Read(device + "/vendor");
            bool amd = vendor == "0x1002" && driver == "amdgpu";
            bool intel = vendor == "0x8086" && driver is "i915" or "xe";
            if ((!amd && !intel) || !seen.Add(pci)) continue;
            string hardware = $"{(amd ? "AMD" : "Intel")} · PCI {pci} · {driver}";

            void Add(string path, string key, string label, SensorKind kind, double divisor, string unit,
                long maximum = long.MaxValue, long minimum = 0)
            {
                cancellation.ThrowIfCancellationRequested();
                long? raw = LinuxFiles.Integer(files.Read(path));
                bool valid = raw.HasValue && raw.Value >= minimum && raw.Value <= maximum;
                rows.Add(new($"drm/{pci}/{key}", hardware, label, kind, HardwareComponent.Gpu,
                    valid ? raw / divisor : null, unit,
                    valid ? null : "Leitura ausente, sem acesso ou inválida no driver."));
            }
            if (amd)
            {
                Add(device + "/gpu_busy_percent", "load", "Uso da GPU", SensorKind.Load, 1, "%", 100);
                Add(device + "/mem_busy_percent", "memoryLoad", "Atividade da memória", SensorKind.Load, 1, "%", 100);
                Add(device + "/mem_info_vram_total", "memoryTotal", "VRAM total", SensorKind.Memory, 1073741824, "GiB", minimum: 1);
                Add(device + "/mem_info_vram_used", "memoryUsed", "VRAM usada", SensorKind.Memory, 1073741824, "GiB");
                var total = rows[^2];
                var used = rows[^1];
                if (total.Value.HasValue && used.Value > total.Value)
                    rows[^1] = used with { Value = null, Error = "VRAM usada excede o total reportado." };
            }
            else if (driver == "i915")
            {
                var gts = files.Directories(card + "/gt", "gt*")
                    .Where(p => Regex.IsMatch(Path.GetFileName(p), @"\Agt[0-9]+\z")).ToArray();
                if (gts.Length == 0)
                {
                    Add(card + "/gt_act_freq_mhz", "actualClock", "Clock gráfico medido", SensorKind.Clock, 1, "MHz", uint.MaxValue);
                    Add(card + "/gt_cur_freq_mhz", "requestedClock", "Clock gráfico solicitado", SensorKind.Clock, 1, "MHz", uint.MaxValue);
                }
                foreach (string gt in gts)
                {
                    string name = Path.GetFileName(gt);
                    Add(gt + "/rps_act_freq_mhz", name + "/actualClock", $"Clock medido · {name}", SensorKind.Clock, 1, "MHz", uint.MaxValue);
                    Add(gt + "/rps_cur_freq_mhz", name + "/requestedClock", $"Clock solicitado · {name}", SensorKind.Clock, 1, "MHz", uint.MaxValue);
                }
            }
            else
            {
                foreach (string tile in files.Directories(device, "tile*"))
                {
                    if (!Regex.IsMatch(Path.GetFileName(tile), @"\Atile[0-9]+\z")) continue;
                    foreach (string gt in files.Directories(tile, "gt*"))
                    {
                        if (!Regex.IsMatch(Path.GetFileName(gt), @"\Agt[0-9]+\z")) continue;
                        string name = Path.GetFileName(tile) + "/" + Path.GetFileName(gt);
                        Add(gt + "/freq0/act_freq", name + "/actualClock", $"Clock medido · {name}", SensorKind.Clock, 1, "MHz", uint.MaxValue);
                        Add(gt + "/freq0/cur_freq", name + "/requestedClock", $"Clock solicitado · {name}", SensorKind.Clock, 1, "MHz", uint.MaxValue);
                    }
                }
            }
        }
        return new(rows.ToArray(), $"DRM: {seen.Count} GPU(s) AMD/Intel; métricas dependem do driver. Clock solicitado e medido podem diferir.");
    }
}
