using System.Globalization;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ResetExplode.Hardware;

/// <summary>Coletor serial: chamar Read em uma única tarefa. Não grava em sysfs nem executa comandos.</summary>
public sealed class LinuxSensorMonitor(LinuxFiles files, IGpuSensorSource? gpu = null) : IDisposable
{
    private static readonly Regex Channel = new(@"^(temp|fan|in|curr|power|freq)(\d+)_(input|average)$",
        RegexOptions.CultureInvariant);
    private ulong[]? _previousCpu;

    public TelemetrySnapshot Read(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        long collectionStarted = Stopwatch.GetTimestamp();
        var sensors = new List<SensorReading>();
        var notices = new List<string>();
        double? load = ReadCpuLoad();
        var memory = LinuxSystemInfoCollector.ReadMemory(files);
        if (load == null) notices.Add("Carga da CPU: aguardando duas leituras válidas de /proc/stat.");
        if (memory.AvailableBytes == null) notices.Add("Memória disponível: informação ausente ou inválida em /proc/meminfo.");

        foreach (string device in files.Directories("/sys/class/hwmon", "hwmon*"))
        {
            cancellation.ThrowIfCancellationRequested();
            string chip = files.Read(device + "/name") ?? Path.GetFileName(device);
            HardwareComponent component = chip switch
            {
                "coretemp" or "k10temp" or "k8temp" or "zenpower" => HardwareComponent.Cpu,
                "amdgpu" or "nouveau" => HardwareComponent.Gpu,
                _ => HardwareComponent.Other
            };
            foreach (string path in files.Files(device, "*"))
            {
                cancellation.ThrowIfCancellationRequested();
                Match match = Channel.Match(Path.GetFileName(path));
                if (!match.Success) continue;
                string type = match.Groups[1].Value;
                string channel = type + match.Groups[2].Value;
                string mode = match.Groups[3].Value;
                if (mode == "average" && type != "power") continue;
                var (kind, divisor, unit) = type switch
                {
                    "temp" => (SensorKind.Temperature, 1000.0, "°C"),
                    "fan" => (SensorKind.Fan, 1.0, "RPM"),
                    "in" => (SensorKind.Voltage, 1000.0, "V"),
                    "curr" => (SensorKind.Current, 1000.0, "A"),
                    "power" => (SensorKind.Power, 1000000.0, "W"),
                    _ => (SensorKind.Clock, 1000000.0, "MHz")
                };
                string label = files.Read(device + "/" + channel + "_label") ?? channel;
                if (mode == "average") label += " (média)";
                long? raw = LinuxFiles.Integer(files.Read(path));
                string? error = raw == null ? "Sem leitura: arquivo ausente, sem acesso ou valor inválido." : null;
                if (files.Read(device + "/" + channel + "_fault") == "1") error = "Falha informada pelo sensor.";
                if (files.Read(device + "/" + channel + "_enable") == "0") error = "Sensor desativado.";
                // temp*_type=4 pode representar termistor em mV, sem conversão térmica disponível.
                if (type == "temp" && files.Read(device + "/" + channel + "_type") == "4")
                    error = "Termistor: conversão específica não implementada.";
                sensors.Add(new(path, $"{chip} · {Path.GetFileName(device)}", label, kind, component,
                    error == null ? raw / divisor : null, unit, error));
            }
        }
        if (sensors.Count == 0) notices.Add("Nenhum sensor hwmon legível foi encontrado.");
        foreach (string policy in files.Directories("/sys/devices/system/cpu/cpufreq", "policy*"))
        {
            cancellation.ThrowIfCancellationRequested();
            long? frequency = LinuxFiles.Integer(files.Read(policy + "/scaling_cur_freq"))
                ?? LinuxFiles.Integer(files.Read(policy + "/cpuinfo_cur_freq"));
            string cpus = files.Read(policy + "/related_cpus") ?? Path.GetFileName(policy);
            sensors.Add(new(policy + "/frequency", "CPU · cpufreq", $"Frequência reportada · CPUs {cpus}",
                SensorKind.Clock, HardwareComponent.Cpu, frequency is >= 0 ? frequency / 1000.0 : null,
                "MHz", frequency is >= 0 ? null : "Frequência indisponível."));
        }
        if (gpu != null)
        {
            var result = gpu.Read(cancellation);
            sensors.AddRange(result.Sensors);
            notices.Add(result.Status);
        }
        return new(DateTimeOffset.Now, load, memory, sensors, notices)
        {
            CollectionStartedTimestamp = collectionStarted
        };
    }

    private double? ReadCpuLoad()
    {
        string? line = files.Read("/proc/stat")?.Split('\n').FirstOrDefault(l => l.StartsWith("cpu ", StringComparison.Ordinal));
        string[] parts = line?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (parts.Length < 5) { _previousCpu = null; return null; }
        var current = new ulong[8];
        // Somente user/nice/system/idle/iowait/irq/softirq/steal; guest já está em user/nice.
        for (int i = 0; i < Math.Min(8, parts.Length - 1); i++)
            if (!ulong.TryParse(parts[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out current[i]))
            { _previousCpu = null; return null; }
        var previous = _previousCpu;
        _previousCpu = current;
        if (previous == null) return null;
        double total = 0, idle = 0;
        for (int i = 0; i < 8; i++)
        {
            // iowait pode regredir; invalidar a amostra e recomeçar a partir da atual.
            if (current[i] < previous[i]) return null;
            double delta = current[i] - previous[i];
            total += delta;
            if (i is 3 or 4) idle += delta;
        }
        return total > 0 ? Math.Clamp(100 * (total - idle) / total, 0, 100) : null;
    }

    public void Dispose() => gpu?.Dispose();
}
