using ResetExplode.Hardware;

internal static class GpuInventoryChecks
{
    public static void Run()
    {
        string root = Directory.CreateTempSubdirectory("reset-gpu-inventory-").FullName;
        try
        {
            void Write(string pci, string field, string value)
            {
                string path = Path.Combine(root, "sys/bus/pci/devices", pci, field);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, value);
            }
            void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
            const string intel = "0000:00:02.0", amd = "0000:03:00.0", nv = "0000:04:00.0";
            var collector = new LinuxGpuInventory(new LinuxFiles(root));
            Check(collector.Collect().Count == 0, "Inventário vazio criou GPU.");
            foreach (string pci in new[] { intel, amd, nv }) Write(pci, "class", "0x030000");
            Write(intel, "vendor", "0x8086"); Write(intel, "device", "0x9bc4");
            Write(intel, "current_link_speed", "Unknown"); Write(intel, "current_link_width", "0");
            Write(amd, "vendor", "0x1002"); Write(amd, "mem_info_vram_total", "8589934592");
            string driver = Path.Combine(root, "sys/bus/pci/drivers/amdgpu"); Directory.CreateDirectory(driver);
            Directory.CreateSymbolicLink(Path.Combine(root, "sys/bus/pci/devices", amd, "driver"), "../../drivers/amdgpu");
            Write(amd, "current_link_speed", "8.0 GT/s PCIe"); Write(amd, "current_link_width", "16");
            Write(nv, "vendor", "0x10de");
            Write("0000:04:00.1", "class", "0x040300");
            var first = collector.Collect(pci =>
            {
                Check(pci == nv, "NVML consultado para outra placa.");
                return new("NVIDIA fixture", 4294967296, "fixture-bios");
            });
            string Value(string pci, string label) => first.Single(g => g.PciAddress == pci).Entries.Single(e => e.Name == label).Value;
            Check(first.Count == 3, "Áudio PCI entrou no inventário GPU.");
            Check(Value(intel, "ID fabricante / dispositivo") == "8086:9bc4", "ID PCI inválido.");
            Check(Value(intel, "Memória reportada pelo driver") == "Indisponível", "RAM virou VRAM integrada.");
            Check(Value(intel, "Link PCIe atual") == "Indisponível", "Link desconhecido virou capacidade válida.");
            Check(Value(amd, "Link PCIe atual") == "8.0 GT/s PCIe · x16", "Link PCIe perdeu unidade ou largura.");
            Check(Value(amd, "Driver do kernel") == "amdgpu", "Link relativo do driver não resolvido.");
            Check(Value(amd, "Memória reportada pelo driver") == $"{8.0:N2} GiB", "VRAM AMD não convertida.");
            Check(Value(nv, "Memória reportada pelo driver") == $"{4.0:N2} GiB", "VRAM NVIDIA não convertida.");
            var vk = new RenderGpu("Intel Vulkan fixture", intel, 0x8086, true) { Kind = "Integrada", ApiVersion = "1.3.0" };
            Check(LinuxGpuInventory.WithVulkan(first, [vk]).Single(g => g.PciAddress == intel).Name == vk.Name,
                "Modelo Vulkan não associado por PCI.");
            Check(LinuxGpuInventory.WithVulkan(first, [vk, vk]).Single(g => g.PciAddress == intel).Name == first.Single(g => g.PciAddress == intel).Name,
                "Vulkan ambíguo associado ao inventário.");
            string physical = Path.Combine(root, "sys/devices/pci0000:00", intel);
            Directory.CreateDirectory(Path.GetDirectoryName(physical)!);
            string intelEntry = Path.Combine(root, "sys/bus/pci/devices", intel);
            Directory.Move(intelEntry, physical);
            Directory.CreateSymbolicLink(intelEntry, "../../../devices/pci0000:00/" + intel);
            Directory.CreateDirectory(Path.Combine(root, "sys/bus/pci/drivers/i915"));
            Directory.CreateSymbolicLink(Path.Combine(physical, "driver"), "../../../bus/pci/drivers/i915");
            Check(collector.Collect().Single(g => g.PciAddress == intel).Entries.Single(e => e.Name == "Driver do kernel").Value == "i915",
                "Driver relativo a dispositivo PCI simbólico não resolvido.");
            Write(amd, "mem_info_vram_total", "-1"); Write(intel, "device", "0x10000");
            var invalid = collector.Collect();
            Check(invalid.Single(g => g.PciAddress == amd).Entries.Single(e => e.Name == "Memória reportada pelo driver").Value == "Indisponível", "VRAM negativa aceita.");
            Check(invalid.Single(g => g.PciAddress == intel).Entries.Single(e => e.Name == "ID fabricante / dispositivo").Value == "Indisponível", "ID fora da faixa aceito.");
            using var stop = new CancellationTokenSource(); stop.Cancel();
            try { collector.Collect(cancellation: stop.Token); throw new Exception("Cancelamento ignorado."); }
            catch (OperationCanceledException) { }
            Directory.Delete(Path.Combine(root, "sys/bus/pci/devices", nv), true);
            Check(collector.Collect().Count == 2 && first.Count == 3, "Remoção manteve GPU antiga ou alterou snapshot.");
            Console.WriteLine("OK: inventário GPU PCI, associação NVML, unidades, ausência, inválidos, remoção e cancelamento.");
        }
        finally { Directory.Delete(root, true); }
    }
}
