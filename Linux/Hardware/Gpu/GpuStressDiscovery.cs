using System.Diagnostics;

namespace ResetExplode.Hardware;

public sealed record GpuStressChoice(RenderGpu Device, bool CanStress, string Status)
{
    public string Label => $"{Device.Name} · {Device.Kind}" + (Device.PciAddress == null ? "" : $" · {Device.PciAddress}");
}

public static class GpuStressDiscovery
{
    public static IReadOnlyList<GpuStressChoice> Collect()
    {
        using var temperature = new LinuxGpuTemperatureSource(new LinuxFiles());
        return Describe(VulkanGpuRenderer.Enumerate(), temperature.Read);
    }

    internal static IReadOnlyList<GpuStressChoice> Describe(IReadOnlyList<RenderGpu> devices, Func<RenderGpu, GpuTemperature> read)
    {
        return devices.Select(device =>
        {
            if (!device.Hardware) return new GpuStressChoice(device, false, "Dispositivo virtual/software: stress não disponível.");
            if (device.PciAddress == null) return new GpuStressChoice(device, false, "Sem identificação PCI: não é possível associar o sensor com segurança.");
            if (devices.Count(d => d.PciAddress == device.PciAddress) != 1)
                return new GpuStressChoice(device, false, "Identificação PCI duplicada; não é possível escolher este dispositivo sem ambiguidade.");
            var reading = read(device);
            try { new GpuThermalGuard(device.PciAddress, 90).Check(reading); }
            catch (InvalidOperationException)
            { return new GpuStressChoice(device, false, reading.Error ?? "Temperatura inválida, antiga ou acima do limite; atualize a lista antes de tentar novamente."); }
            return new GpuStressChoice(device, true, $"Sensor da GPU disponível: {reading.Celsius:N0} °C na detecção. Será verificado novamente ao iniciar.");
        }).ToArray();
    }

    // Preserva apenas a identidade explícita; nunca troca para outra GPU se a selecionada desaparecer.
    public static GpuStressChoice? RestoreSelection(IEnumerable<GpuStressChoice> choices, string? pciAddress) =>
        pciAddress == null ? null : choices.SingleOrDefault(c => c.Device.PciAddress == pciAddress && c.CanStress)
            ?? choices.FirstOrDefault(c => c.Device.PciAddress == pciAddress);
}

/// <summary>Leitura serial do dispositivo PCI escolhido. Não usa temperatura da CPU como substituta da GPU.</summary>
public sealed class LinuxGpuTemperatureSource(LinuxFiles files) : IDisposable
{
    private readonly LinuxNvml _nvml = new();
    public GpuTemperature Read(RenderGpu device)
    {
        string pci = device.PciAddress ?? "";
        _ = new GpuThermalGuard(pci, 90);
        long started = Stopwatch.GetTimestamp();
        if (device.VendorId == 0x10de) return _nvml.ReadTemperature(pci);
        // hwmon sob o próprio dispositivo PCI, não a lista global de sensores.
        double? hottest = null;
        foreach (string hwmon in files.Directories($"/sys/bus/pci/devices/{pci}/hwmon", "hwmon*"))
        {
            string? driver = files.Read(hwmon + "/name");
            if (driver is not ("amdgpu" or "radeon" or "i915" or "xe")) continue;
            foreach (string sensor in files.Files(hwmon, "temp*_input"))
            {
                string stem = sensor[..^6];
                if (files.Read(stem + "_fault") == "1" || files.Read(stem + "_enable") == "0" || files.Read(stem + "_type") == "4")
                    return new(pci, null, started, "Sensor térmico da GPU desativado ou com falha.");
                long? raw = LinuxFiles.Integer(files.Read(sensor));
                if (raw is null or < 0 or > 150000) return new(pci, null, started, "Leitura térmica da GPU inválida.");
                hottest = Math.Max(hottest ?? 0, raw.Value / 1000.0);
            }
        }
        return new(pci, hottest, started, hottest == null
            ? "O driver não expõe temperatura própria desta GPU. Stress indisponível; a temperatura da CPU não será usada como substituta." : null);
    }
    public void Dispose() => _nvml.Dispose();
}
