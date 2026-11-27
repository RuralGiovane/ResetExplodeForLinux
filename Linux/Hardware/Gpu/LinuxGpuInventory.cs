using System.Globalization;
using System.Text.RegularExpressions;

namespace ResetExplode.Hardware;

public sealed record GpuSpecifications(string PciAddress, string Name, IReadOnlyList<SystemEntry> Entries);
public sealed record NvidiaSpecifications(string? Name, ulong? MemoryBytes, string? Vbios)
{
    public uint? GpuCores { get; init; }
    public uint? MemoryBusWidthBits { get; init; }
    public uint? MaxGraphicsClockMhz { get; init; }
    public uint? MaxMemoryClockMhz { get; init; }
}

/// <summary>Inventário PCI somente de leitura, independente de Vulkan e de sensores térmicos.</summary>
public sealed class LinuxGpuInventory(LinuxFiles files)
{
    public IReadOnlyList<GpuSpecifications> Collect(
        Func<string, NvidiaSpecifications?>? nvidia = null, CancellationToken cancellation = default)
    {
        var result = new List<GpuSpecifications>();
        foreach (string entryPath in files.Directories("/sys/bus/pci/devices", "*"))
        {
            cancellation.ThrowIfCancellationRequested();
            string pci = Path.GetFileName(entryPath);
            // Resolve the PCI class link before its driver link, which is relative to the physical device.
            string path = files.DirectoryTarget(entryPath) ?? entryPath;
            if (!Regex.IsMatch(pci, @"\A[0-9a-fA-F]{4}:[0-9a-fA-F]{2}:[0-9a-fA-F]{2}\.[0-7]\z")) continue;
            uint? Hex(string field, uint max) => ParseHex(files.Read(path + "/" + field), max);
            if (Hex("class", 0xffffff) is not uint cls || (cls >> 16) != 3) continue;
            var rows = new List<SystemEntry>();
            void Add(string label, string? value) => rows.Add(new(label,
                string.IsNullOrWhiteSpace(value) ? "Indisponível" : value));
            uint? vendor = Hex("vendor", 0xffff);
            string manufacturer = vendor switch { 0x10de => "NVIDIA", 0x1002 => "AMD", 0x8086 => "Intel", _ => "Fabricante não identificado" };
            string? driverPath = files.DirectoryTarget(path + "/driver");
            string? driver = driverPath == null ? null : Path.GetFileName(driverPath);
            var nv = vendor == 0x10de ? nvidia?.Invoke(pci) : null;
            string? product = nv?.Name ?? files.Read(path + "/product_name");
            string name = product ?? $"{manufacturer} · PCI {pci}";
            Add("Modelo reportado", product);
            Add("Fabricante", manufacturer);
            Add("Endereço PCI", pci);
            Add("ID fabricante / dispositivo", vendor is uint v && Hex("device", 0xffff) is uint d ? $"{v:x4}:{d:x4}" : null);
            Add("ID do subsistema", Hex("subsystem_vendor", 0xffff) is uint sv && Hex("subsystem_device", 0xffff) is uint sd ? $"{sv:x4}:{sd:x4}" : null);
            Add("Revisão PCI", Hex("revision", 0xff)?.ToString("x2"));
            Add("Driver do kernel", driver);
            Add("Versão do módulo", files.Read(path + "/driver/module/version"));
            Add("VBIOS", nv?.Vbios ?? (driver == "amdgpu" ? files.Read(path + "/vbios_version") : null));
            string? Link(string prefix)
            {
                string? speed = files.Read(path + "/" + prefix + "_link_speed");
                long? width = LinuxFiles.Integer(files.Read(path + "/" + prefix + "_link_width"));
                return speed != null && Regex.IsMatch(speed, @"\A[0-9]+(?:\.[0-9]+)? GT/s(?: PCIe)?\z") && width is > 0 and <= 32
                    ? $"{speed} · x{width}" : null;
            }
            Add("Link PCIe atual", Link("current"));
            Add("Link PCIe máximo reportado", Link("max"));
            ulong? memory = nv?.MemoryBytes;
            if (vendor == 0x1002 && driver == "amdgpu" && LinuxFiles.Integer(files.Read(path + "/mem_info_vram_total")) is >= 0 and var bytes)
                memory = (ulong)bytes;
            Add("Memória reportada pelo driver", memory is ulong m ? $"{m / 1073741824.0:N2} GiB" : null);
            // These are independent optional queries, never estimates from model names or clocks.
            string? Positive(uint? value, string unit = "") => value is > 0 and < uint.MaxValue
                ? value.Value.ToString(CultureInfo.CurrentCulture) + unit : null;
            Add("Núcleos GPU (NVML)", Positive(nv?.GpuCores));
            Add("Barramento de memória (NVML)", Positive(nv?.MemoryBusWidthBits, " bits"));
            Add("Clock gráfico máximo reportado (NVML)", Positive(nv?.MaxGraphicsClockMhz, " MHz"));
            Add("Clock de memória máximo reportado (NVML)", Positive(nv?.MaxMemoryClockMhz, " MHz"));
            Add("Tipo de memória", null);
            Add("Processo de fabricação (litografia)", null);
            Add("Fontes", nv != null ? "PCI/sysfs + NVML (associação por PCI)" : "PCI/sysfs");
            result.Add(new(pci, name, rows));
        }
        return result;
    }

    private static uint? ParseHex(string? text, uint max) => text?.StartsWith("0x", StringComparison.Ordinal) == true &&
        uint.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint value) && value <= max ? value : null;

    public static IReadOnlyList<GpuSpecifications> WithVulkan(IReadOnlyList<GpuSpecifications> inventory, IReadOnlyList<RenderGpu> devices) =>
        inventory.Select(gpu =>
        {
            var matches = devices.Where(d => d.Hardware && d.PciAddress == gpu.PciAddress).ToArray();
            if (matches.Length != 1) return gpu;
            var device = matches[0];
            return gpu with { Name = device.Name, Entries = gpu.Entries.Select(entry => entry.Name switch
            {
                "Modelo reportado" => new SystemEntry("Modelo (Vulkan)", device.Name),
                "Fontes" => entry with { Value = entry.Value + " + Vulkan (associação por PCI)" },
                _ => entry
            }).Concat(new SystemEntry[]
            {
                new("Tipo Vulkan", device.Kind),
                new("API Vulkan", device.ApiVersion ?? "Indisponível")
            }).ToArray() };
        }).ToArray();

    public static IReadOnlyList<GpuSpecifications> Read()
    {
        using var nvml = new LinuxNvml();
        return new LinuxGpuInventory(new LinuxFiles()).Collect(nvml.ReadSpecifications);
    }
}
