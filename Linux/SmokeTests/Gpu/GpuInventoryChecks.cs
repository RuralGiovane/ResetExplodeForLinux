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
                return new("NVIDIA fixture", 4294967296, "fixture-bios")
                {
                    GpuCores = 896, MemoryBusWidthBits = 128,
                    MaxGraphicsClockMhz = 2130, MaxMemoryClockMhz = 4001
                };
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
            Check(Value(nv, "Núcleos GPU (NVML)") == "896", "Contagem de núcleos perdida.");
            Check(Value(nv, "Barramento de memória (NVML)") == "128 bits", "Barramento de memória confundido com PCIe.");
            Check(Value(nv, "Clock gráfico máximo reportado (NVML)") == "2130 MHz", "Clock gráfico máximo perdido.");
            Check(Value(nv, "Clock de memória máximo reportado (NVML)") == "4001 MHz", "Clock convertido indevidamente em taxa efetiva.");
            foreach (string pci in new[] { intel, amd })
                Check(Value(pci, "Núcleos GPU (NVML)") == "Indisponível" && Value(pci, "Barramento de memória (NVML)") == "Indisponível",
                    "Especificações NVIDIA atribuídas a outra GPU.");
            Check(Value(nv, "Tipo de memória") == "Indisponível" && Value(nv, "Processo de fabricação (litografia)") == "Indisponível",
                "Especificação comercial inferida sem fonte.");
            var partial = collector.Collect(_ => new("NVIDIA fixture", 4294967296, "fixture-bios")
            {
                GpuCores = 0, MemoryBusWidthBits = uint.MaxValue, MaxMemoryClockMhz = 4001
            }).Single(g => g.PciAddress == nv);
            Check(partial.Entries.Single(e => e.Name == "Núcleos GPU (NVML)").Value == "Indisponível" &&
                partial.Entries.Single(e => e.Name == "Barramento de memória (NVML)").Value == "Indisponível" &&
                partial.Entries.Single(e => e.Name == "Clock gráfico máximo reportado (NVML)").Value == "Indisponível" &&
                partial.Entries.Single(e => e.Name == "Clock de memória máximo reportado (NVML)").Value == "4001 MHz",
                "Ausência/valor inválido contaminou especificações independentes.");
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
            // Reference data require exact documented IDs; telemetry must remain independent.
            foreach (var sample in new[]
            {
                (Id: "0x1b06", Name: "NVIDIA GeForce GTX 1080 Ti", Memory: "GDDR5X (referência)", Process: "16 nm (referência)"),
                (Id: "0x1e04", Name: "NVIDIA GeForce RTX 2080 Ti", Memory: "GDDR6 (referência)", Process: "12 nm FFN (referência)"),
                (Id: "0x1e07", Name: "geforce rtx 2080 ti", Memory: "GDDR6 (referência)", Process: "12 nm FFN (referência)")
            })
            {
                Write(nv, "device", sample.Id);
                var referenced = collector.Collect(_ => new(sample.Name, 4294967296, null)).Single(g => g.PciAddress == nv);
                string Ref(string label) => referenced.Entries.Single(e => e.Name == label).Value;
                Check(Ref("Tipo de memória") == sample.Memory && Ref("Processo de fabricação (litografia)") == sample.Process,
                    "Referência documentada não associada pelo PCI.");
                Check(Ref("Fonte das especificações de referência").StartsWith("https://developer.nvidia.com/", StringComparison.Ordinal) &&
                    Ref("Fonte da identificação PCI").StartsWith("https://download.nvidia.com/", StringComparison.Ordinal), "Referência sem proveniência.");
                Check(Ref("Memória reportada pelo driver") == $"{4.0:N2} GiB" && Ref("Núcleos GPU (NVML)") == "Indisponível",
                    "Catálogo substituiu medição do driver ou inventou núcleos.");
                Check(collector.Collect().Single(g => g.PciAddress == nv).Entries.Single(e => e.Name == "Tipo de memória").Value == sample.Memory,
                    "Catálogo PCI passou a exigir NVML.");
            }
            foreach (string conflicting in new[] { "GeForce RTX 2080", "GeForce RTX 2080 Ti SUPER", "GeForce RTX 2080 Ti Laptop GPU" })
                Check(collector.Collect(_ => new(conflicting, null, null)).Single(g => g.PciAddress == nv).Entries
                    .Single(e => e.Name == "Tipo de memória").Value == "Indisponível", "Nome conflitante aceito por semelhança.");
            Write(nv, "device", "0x1f9d");
            Check(collector.Collect(_ => new("NVIDIA GeForce GTX 1650", 4294967296, null) { MaxMemoryClockMhz = 6001 })
                .Single(g => g.PciAddress == nv).Entries.Single(e => e.Name == "Tipo de memória").Value == "Indisponível",
                "GTX 1650 ambígua recebeu tipo de memória pelo clock ou nome.");
            Write(amd, "device", "0x1e07");
            Check(collector.Collect().Single(g => g.PciAddress == amd).Entries.Single(e => e.Name == "Tipo de memória").Value == "Indisponível",
                "Catálogo ignorou o fabricante PCI.");
            Directory.Delete(Path.Combine(root, "sys/bus/pci/devices", nv), true);
            Check(collector.Collect().Count == 2 && first.Count == 3, "Remoção manteve GPU antiga ou alterou snapshot.");
            Console.WriteLine("OK: inventário GPU PCI, associação NVML, unidades, ausência, inválidos, remoção e cancelamento.");
        }
        finally { Directory.Delete(root, true); }
    }
}
