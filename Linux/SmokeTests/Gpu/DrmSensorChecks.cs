using System.Globalization;
using ResetExplode.Hardware;

internal static class DrmSensorChecks
{
    public static void Run()
    {
        string root = Directory.CreateTempSubdirectory("reset-drm-").FullName;
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            string Real(string path) => Path.Combine(root, path.TrimStart('/'));
            void Write(string path, string value)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Real(path))!);
                File.WriteAllText(Real(path), value);
            }
            void Link(string path, string target)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Real(path))!);
                Directory.CreateSymbolicLink(Real(path), Real(target));
            }
            string Device(int card, string pci, string vendor, string driver)
            {
                string device = "/sys/devices/pci0000:00/" + pci;
                Write(device + "/vendor", vendor);
                Directory.CreateDirectory(Real("/sys/bus/pci/drivers/" + driver));
                Link(device + "/driver", "/sys/bus/pci/drivers/" + driver);
                Link($"/sys/class/drm/card{card}/device", device);
                return device;
            }
            void Check(bool value, string message) { if (!value) throw new Exception(message); }
            var collector = new LinuxDrmSensors(new LinuxFiles(root));
            Check(collector.Read().Sensors.Count == 0, "DRM ausente gerou leituras.");
            string amd = Device(0, "0000:03:00.0", "0x1002", "amdgpu");
            Write(amd + "/gpu_busy_percent", "0");
            Write(amd + "/mem_busy_percent", "54");
            Write(amd + "/mem_info_vram_total", "8589934592");
            Write(amd + "/mem_info_vram_used", "2147483648");
            Device(1, "0000:00:02.0", "0x8086", "i915");
            Write("/sys/class/drm/card1/gt_act_freq_mhz", "350");
            Write("/sys/class/drm/card1/gt_cur_freq_mhz", "600");
            var first = collector.Read();
            SensorReading Row(GpuSnapshot s, string id) => s.Sensors.Single(x => x.Id == "drm/" + id);
            Check(Row(first, "0000:03:00.0/load").Value == 0, "Uso zero virou ausência.");
            Check(Row(first, "0000:03:00.0/memoryTotal").Value == 8 && Row(first, "0000:03:00.0/memoryUsed").Value == 2, "Escala VRAM incorreta.");
            Check(Row(first, "0000:00:02.0/actualClock").Value == 350 && Row(first, "0000:00:02.0/requestedClock").Value == 600, "Clocks medido/solicitado confundidos.");
            Link("/sys/class/drm/card8/device", amd);
            Link("/sys/class/drm/card0-HDMI-A-1/device", amd);
            Check(collector.Read().Sensors.Count == 6, "Duplicou PCI ou conector DRM.");
            Write(amd + "/gpu_busy_percent", "101");
            Write(amd + "/mem_busy_percent", "-1");
            Write(amd + "/mem_info_vram_used", "9999999999");
            Write("/sys/class/drm/card1/gt_act_freq_mhz", "99999999999999999999999");
            Write("/sys/class/drm/card1/gt_cur_freq_mhz", "3,5");
            var invalid = collector.Read();
            Check(invalid.Sensors.Count(x => x.Value != null) == 1, "Dados inválidos aceitos.");
            Check(Row(first, "0000:03:00.0/load").Value == 0, "Snapshot anterior foi alterado.");
            Write(amd + "/mem_info_vram_used", "0");
            File.Delete(Real(amd + "/gpu_busy_percent"));
            Check(Row(collector.Read(), "0000:03:00.0/load").Value == null, "Arquivo removido manteve valor antigo.");
            Write("/sys/class/drm/card1/gt/gt0/rps_act_freq_mhz", "450");
            Write("/sys/class/drm/card1/gt/gt0/rps_cur_freq_mhz", "700");
            Write("/sys/class/drm/card1/gt/gt1/rps_act_freq_mhz", "0");
            var multi = collector.Read();
            Check(Row(multi, "0000:00:02.0/gt0/actualClock").Value == 450 && Row(multi, "0000:00:02.0/gt1/actualClock").Value == 0, "GTs confundidos ou zero perdido.");
            Check(!multi.Sensors.Any(x => x.Id == "drm/0000:00:02.0/actualClock"), "Somou interface legada com GTs.");
            string xe = Device(2, "0000:04:00.0", "0x8086", "xe");
            Write(xe + "/tile0/gt0/freq0/act_freq", "900");
            Write(xe + "/tile0/gt0/freq0/cur_freq", "1000");
            Check(Row(collector.Read(), "0000:04:00.0/tile0/gt0/actualClock").Value == 900, "Layout Xe não lido.");
            Device(3, "0000:05:00.0", "0x10de", "nvidia");
            Check(!collector.Read().Sensors.Any(x => x.Id.Contains("0000:05:00.0")), "Duplicou backend NVML.");
            Device(4, "0000:06:00.0", "0x8086", "amdgpu");
            Check(!collector.Read().Sensors.Any(x => x.Id.Contains("0000:06:00.0")), "Fabricante/driver incompatíveis aceitos.");
            Directory.Move(Real("/sys/class/drm/card1"), Real("/sys/class/drm/card7"));
            Check(Row(collector.Read(), "0000:00:02.0/gt0/actualClock").Value == 450, "Renumerar card mudou identidade PCI.");
            string physicalCard = "/sys/devices/pci0000:00/0000:00:02.0/drm/card7";
            Directory.CreateDirectory(Real("/sys/devices/pci0000:00/0000:00:02.0/drm"));
            Directory.Move(Real("/sys/class/drm/card7"), Real(physicalCard));
            Directory.Delete(Real(physicalCard + "/device"));
            Directory.CreateSymbolicLink(Real(physicalCard + "/device"), "../../../0000:00:02.0");
            Link("/sys/class/drm/card7", physicalCard);
            Check(Row(collector.Read(), "0000:00:02.0/gt0/actualClock").Value == 450,
                "Link relativo device dentro do card simbólico não foi resolvido fisicamente.");
            Directory.Delete(Real(amd), true);
            Check(!collector.Read().Sensors.Any(x => x.Id.Contains("0000:03:00.0")), "GPU removida manteve leituras.");
            using var monitor = new LinuxSensorMonitor(new LinuxFiles(root));
            Check(monitor.Read().Sensors.Any(x => x.Id.StartsWith("drm/")), "DRM não integrado à telemetria.");
            Write("/sys/class/hwmon/hwmon0/name", "i915");
            Write("/sys/class/hwmon/hwmon0/temp1_input", "47000");
            var thermal = monitor.Read();
            Check(thermal.CpuTemperature == null && thermal.Sensors.Any(x => x.Kind == SensorKind.Temperature &&
                x.Component == HardwareComponent.Gpu && x.Value == 47), "Sensor Intel atribuído à CPU ou não classificado como GPU.");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { collector.Read(cancelled.Token); throw new Exception("Cancelamento ignorado."); }
            catch (OperationCanceledException) { }
        }
        finally { CultureInfo.CurrentCulture = culture; Directory.Delete(root, true); }
        Console.WriteLine("OK: DRM AMD/Intel, PCI, VRAM, clocks i915/Xe, multi-GT, cultura, ausência, limites, hotplug e cancelamento.");
    }
}
