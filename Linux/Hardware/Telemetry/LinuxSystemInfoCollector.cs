using System.Globalization;
using System.Runtime.InteropServices;

namespace ResetExplode.Hardware;

public sealed class LinuxSystemInfoCollector(LinuxFiles files)
{
    public LinuxSystemInfo Collect()
    {
        var entries = new List<SystemEntry>();
        void Add(string name, string? value) => entries.Add(new(name,
            string.IsNullOrWhiteSpace(value) ? "Indisponível" : value));

        string? release = files.Read("/etc/os-release") ?? files.Read("/usr/lib/os-release");
        string? prettyName = release?.Split('\n')
            .FirstOrDefault(l => l.StartsWith("PRETTY_NAME=", StringComparison.Ordinal));
        Add("Distribuição", prettyName?["PRETTY_NAME=".Length..].Trim().Trim('"', '\''));
        Add("Kernel", files.Read("/proc/sys/kernel/osrelease"));
        Add("Arquitetura do processo", RuntimeInformation.ProcessArchitecture.ToString());
        string? cpuInfo = files.Read("/proc/cpuinfo");
        string? cpu = cpuInfo?.Split('\n').Select(l => l.Split(':', 2))
            .FirstOrDefault(p => p.Length == 2 && (p[0].Trim() is "model name" or "Hardware"))?[1].Trim();
        Add("Processador", cpu);
        // Environment.ProcessorCount respeita afinidade/limites do processo; não é contagem física.
        Add("Threads disponíveis ao processo", Environment.ProcessorCount.ToString(CultureInfo.CurrentCulture));
        Add("Fabricante da placa-mãe", files.Read("/sys/class/dmi/id/board_vendor"));
        Add("Placa-mãe", files.Read("/sys/class/dmi/id/board_name"));
        Add("Fabricante da BIOS", files.Read("/sys/class/dmi/id/bios_vendor"));
        Add("BIOS", files.Read("/sys/class/dmi/id/bios_version"));
        Add("Data da BIOS", files.Read("/sys/class/dmi/id/bios_date"));
        var memory = ReadMemory(files);
        Add("RAM total do sistema", memory.TotalBytes is long total ? $"{total / 1073741824.0:N2} GiB" : null);
        return new(entries);
    }

    public static MemoryStatus ReadMemory(LinuxFiles files)
    {
        var values = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (string line in (files.Read("/proc/meminfo") ?? "").Split('\n'))
        {
            var pair = line.Split(':', 2);
            if (pair.Length != 2) continue;
            var parts = pair[1].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || parts[1] != "kB") continue;
            long? kb = LinuxFiles.Integer(parts[0]);
            if (kb is >= 0 && kb <= long.MaxValue / 1024) values[pair[0]] = kb.Value * 1024;
        }
        long? total = values.TryGetValue("MemTotal", out long t) && t > 0 ? t : null;
        long? available = values.TryGetValue("MemAvailable", out long a) && total is long max && a <= max ? a : null;
        // MemFree não substitui MemAvailable: cache recuperável altera a interpretação.
        return new(total, available);
    }
}
