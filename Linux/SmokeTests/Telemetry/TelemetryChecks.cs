using System.Globalization;
using System.Diagnostics;
using ResetExplode.Hardware;

internal static class TelemetryChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "reset-telemetry-" + Guid.NewGuid().ToString("N"));
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            void Write(string path, string value)
            {
                string target = Path.Combine(root, path.TrimStart('/'));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, value);
            }
            var files = new LinuxFiles(root);
            var gpu = new FakeGpu();
            using (var monitor = new LinuxSensorMonitor(files, gpu))
            {
                var empty = monitor.Read();
                Check(empty.CpuLoadPercent == null && empty.Memory.UsedBytes == null && empty.CpuTemperature == null,
                    "Arquivos ausentes viraram métricas iguais a zero.");
                Check(empty.Notices.Count > 0, "Ausência de fontes não foi informada.");

                Write("/etc/os-release", "NAME=Teste\nPRETTY_NAME=\"Linux de Teste\"\n");
                Write("/proc/cpuinfo", "processor : 0\nmodel name : CPU de Teste\n");
                Write("/proc/sys/kernel/osrelease", "6.0-teste\n");
                Write("/sys/class/dmi/id/board_name", "Placa de Teste\n");
                Write("/proc/meminfo", "MemTotal: 8388608 kB\nMemAvailable: 2097152 kB\nMemFree: 10 kB\n");
                Write("/proc/stat", "cpu 100 0 100 800 0 0 0 0 50 0\ncpu0 100 0 100 800\n");
                Write("/sys/class/hwmon/hwmon0/name", "coretemp");
                Write("/sys/class/hwmon/hwmon0/temp1_input", "42500");
                Write("/sys/class/hwmon/hwmon0/temp1_label", "Package id 0");
                Write("/sys/class/hwmon/hwmon0/temp2_input", "-5000");
                Write("/sys/class/hwmon/hwmon0/temp3_input", "99000");
                Write("/sys/class/hwmon/hwmon0/temp3_fault", "1");
                Write("/sys/class/hwmon/hwmon0/temp4_input", "90000");
                Write("/sys/class/hwmon/hwmon0/temp4_enable", "0");
                Write("/sys/class/hwmon/hwmon0/fan1_input", "0");
                Write("/sys/class/hwmon/hwmon0/in0_input", "1200");
                Write("/sys/class/hwmon/hwmon0/curr1_input", "1500");
                Write("/sys/class/hwmon/hwmon0/power1_average", "65000000");
                Write("/sys/class/hwmon/hwmon0/freq1_input", "4200000000");
                Write("/sys/class/hwmon/hwmon1/name", "acpitz");
                Write("/sys/class/hwmon/hwmon1/temp1_input", "110000");
                Write("/sys/class/hwmon/hwmon1/temp2_input", "inválido");
                Write("/sys/devices/system/cpu/cpufreq/policy0/scaling_cur_freq", "3200000");
                Write("/sys/devices/system/cpu/cpufreq/policy0/related_cpus", "0 1");

                var first = monitor.Read();
                Check(first.CollectionStartedTimestamp > 0 && first.CollectionStartedTimestamp <= gpu.LastReadTimestamp,
                    "A idade térmica não foi marcada antes da consulta de GPU.");
                SensorReading Sensor(string suffix) => first.Sensors.Single(s => s.Id.EndsWith(suffix, StringComparison.Ordinal));
                Check(first.CpuLoadPercent == null, "A primeira amostra de CPU não deveria produzir carga.");
                Check(first.Memory.TotalBytes == 8L * 1024 * 1024 * 1024 && first.Memory.UsagePercent == 75,
                    "RAM não foi convertida de kB para bytes ou usou MemFree indevidamente.");
                Check(first.CpuTemperature == 42.5, "Resumo térmico usou sensor ACPI, desativado ou em falha.");
                Check(first.Sensors.Any(s => s.Id == "fake/gpu/temperature" && s.Value == 95),
                    "Leituras do backend de GPU não foram agregadas.");
                Check(Sensor("hwmon0/temp2_input").Value == -5, "Temperatura negativa válida foi perdida.");
                Check(Sensor("hwmon0/fan1_input").Value == 0, "Ventoinha parada foi confundida com ausência de leitura.");
                Check(Sensor("hwmon0/in0_input").Value == 1.2 && Sensor("hwmon0/curr1_input").Value == 1.5,
                    "Escala de tensão/corrente incorreta.");
                Check(Sensor("hwmon0/power1_average").Value == 65 && Sensor("hwmon0/freq1_input").Value == 4200,
                    "Escala de potência/frequência incorreta.");
                Check(Sensor("policy0/frequency").Value == 3200, "Clock cpufreq não foi convertido de kHz para MHz.");
                Check(Sensor("hwmon1/temp2_input").Value == null && Sensor("hwmon1/temp2_input").Error != null,
                    "Valor inválido não ficou explicitamente indisponível.");
                var info = new LinuxSystemInfoCollector(files).Collect();
                Check(info.Entries.Any(e => e.Name == "Distribuição" && e.Value == "Linux de Teste"), "os-release não foi lido.");
                Check(info.Entries.Any(e => e.Name == "Processador" && e.Value == "CPU de Teste"), "CPU não foi identificada.");
                Check(info.Entries.Any(e => e.Name == "BIOS" && e.Value == "Indisponível"), "DMI ausente virou dado fictício.");

                // Delta total 100, ocupado 40; guest não deve ser somado novamente.
                Write("/proc/stat", "cpu 120 0 120 860 0 0 0 0 999999 0\n");
                Check(monitor.Read().CpuLoadPercent == 40, "Carga incorreta ou guest contado duas vezes.");
                Check(monitor.Read().CpuLoadPercent == null, "Delta zero não deveria produzir carga.");
                Write("/proc/stat", "cpu 1 0 1 8 0 0 0 0\n");
                Check(monitor.Read().CpuLoadPercent == null, "Regressão de contadores não foi descartada.");
                Write("/proc/stat", "cpu 2 0 2 16 10 0 0 0\n");
                monitor.Read();
                Write("/proc/stat", "cpu 3 0 3 24 9 0 0 0\n");
                Check(monitor.Read().CpuLoadPercent == null, "Regressão apenas de iowait não foi descartada.");
                Write("/proc/meminfo", "MemTotal: 1024 kB\nMemAvailable: 2048 kB\n");
                Check(monitor.Read().Memory.UsedBytes == null, "MemAvailable maior que total gerou RAM usada negativa.");
                Write("/proc/meminfo", "MemTotal: 1024 kB\nMemFree: 10 kB\n");
                Check(monitor.Read().Memory.AvailableBytes == null, "MemFree substituiu MemAvailable.");
                Directory.Delete(Path.Combine(root, "sys/class/hwmon/hwmon0"), true);
                Check(monitor.Read().CpuTemperature == null, "Remoção do sensor manteve uma temperatura antiga.");

                using var cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                bool cancellationObserved = false;
                try { monitor.Read(cancelled.Token); }
                catch (OperationCanceledException) { cancellationObserved = true; }
                Check(cancellationObserved, "Coleta ignorou cancelamento.");
            }
            Check(gpu.Disposed, "Fonte de GPU não foi liberada.");
            Console.WriteLine("OK: telemetria com fixtures (ausência, unidades, cultura, CPU, RAM, DMI, hotplug e cancelamento).");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class FakeGpu : IGpuSensorSource
    {
        public bool Disposed { get; private set; }
        public long LastReadTimestamp { get; private set; }
        public GpuSnapshot Read(CancellationToken cancellation)
        {
            LastReadTimestamp = Stopwatch.GetTimestamp();
            return new([new("fake/gpu/temperature", "GPU simulada", "Temperatura", SensorKind.Temperature,
                HardwareComponent.Gpu, 95, "°C")], "GPU simulada para testar agregação.");
        }
        public void Dispose() => Disposed = true;
    }
}
