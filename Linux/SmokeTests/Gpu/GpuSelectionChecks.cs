using System.Diagnostics;
using ResetExplode.Hardware;

internal static class GpuSelectionChecks
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static void Run()
    {
        var integrated = new RenderGpu("Intel", "0000:00:02.0", 0x8086, true) { Kind = "Integrada" };
        var dedicated = new RenderGpu("NVIDIA", "0000:01:00.0", 0x10de, true) { Kind = "Dedicada" };
        GpuTemperature Read(RenderGpu d) => new(d.PciAddress!, d == integrated ? null : 50, Stopwatch.GetTimestamp(), d == integrated ? "Sem sensor" : null);
        var choices = GpuStressDiscovery.Describe([integrated, dedicated], Read);
        Check(choices.Count == 2 && !choices[0].CanStress && choices[1].CanStress, "Detecção não preservou integrada indisponível/dedicada disponível.");
        Check(GpuStressDiscovery.RestoreSelection(choices, null) == null, "Selecionou GPU sem escolha explícita.");
        Check(GpuStressDiscovery.RestoreSelection(choices.Reverse(), dedicated.PciAddress)?.Device == dedicated, "Troca de ordem mudou seleção.");
        Check(GpuStressDiscovery.RestoreSelection(choices.Take(1), dedicated.PciAddress) == null, "GPU removida virou outra GPU.");
        Check(GpuStressDiscovery.RestoreSelection(choices, integrated.PciAddress)?.Device == integrated, "GPU sem sensor desapareceu da seleção.");
        var ambiguous = GpuStressDiscovery.Describe([dedicated, dedicated with { Name = "Outro driver" }], _ => throw new Exception("Não deveria ler sensor ambíguo"));
        Check(ambiguous.All(d => !d.CanStress), "PCI duplicado aceito para carga.");
        Check(!GpuStressDiscovery.Describe([integrated with { Hardware = false }], _ => throw new Exception())[0].CanStress, "Software aceito.");
        Check(!GpuStressDiscovery.Describe([integrated with { PciAddress = null }], _ => throw new Exception())[0].CanStress, "GPU sem PCI aceita.");
        Check(!GpuStressDiscovery.Describe([dedicated], _ => new(integrated.PciAddress!, 50, Stopwatch.GetTimestamp(), null))[0].CanStress, "Sensor de outra GPU aceito na descoberta.");
        string root = Directory.CreateTempSubdirectory("reset-gpu-sensor-").FullName;
        try
        {
            string folder = Path.Combine(root, "sys/bus/pci/devices/0000:00:02.0/hwmon/hwmon0");
            Directory.CreateDirectory(folder);
            void Write(string name, string value) => File.WriteAllText(Path.Combine(folder, name), value);
            Write("name", "i915"); Write("temp1_input", "45000"); Write("temp2_input", "52000");
            using var source = new LinuxGpuTemperatureSource(new LinuxFiles(root));
            Check(source.Read(integrated).Celsius == 52, "Não converteu/selecionou maior temperatura da própria GPU.");
            var other = integrated with { PciAddress = "0000:02:00.0" };
            Check(source.Read(other).Celsius == null, "Atribuiu sensor de outra placa.");
            Write("temp1_fault", "1"); Check(source.Read(integrated).Celsius == null, "Falha no sensor ignorada.");
            Write("temp1_fault", "0"); Write("temp1_enable", "0"); Check(source.Read(integrated).Celsius == null, "Sensor desativado aceito.");
            Write("temp1_enable", "1"); Write("temp1_input", "invalid"); Check(source.Read(integrated).Celsius == null, "Leitura inválida aceita.");
            Write("temp1_input", "45000"); Write("name", "coretemp"); Check(source.Read(integrated).Celsius == null, "Sensor CPU aceito como GPU.");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine("OK: seleção multi-GPU, identidade PCI, reordenação/remoção, indisponibilidade e sensores hwmon por dispositivo.");
    }
}
