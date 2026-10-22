using System.Globalization;
using ResetExplode.Hardware;

internal static class StorageInventoryChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "reset-block-" + Guid.NewGuid().ToString("N"));
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            void Write(string path, string value)
            {
                string target = Path.Combine(root, "sys/block", path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, value);
            }
            var collector = new LinuxStorageCollector(new LinuxFiles(root));
            Check(collector.Collect().Devices.Count == 0 && collector.Collect().Notices.Count > 0, "Ausência de sysfs não foi informada.");
            Write("nvme0n1/size", "2097152");
            Write("nvme0n1/device/model", "NVMe de teste");
            Write("nvme0n1/queue/logical_block_size", "4096");
            Write("nvme0n1/queue/rotational", "0");
            Write("nvme0n1/removable", "0");
            Write("nvme0n1/ro", "0");
            Write("nvme0n1/nvme0n1p1/partition", "1");
            Write("nvme0n1/nvme0n1p1/size", "1024");
            Write("nvme0n1/nvme0n1p1/ro", "1");
            Write("sda/size", "9223372036854775807");
            Write("sda/queue/rotational", "1");
            Write("sda/removable", "2");
            Write("loop0/size", "0");
            Write("dm-0/size", "-1");
            var snapshot = collector.Collect();
            Check(snapshot.Devices.Count == 5, "Diretórios auxiliares viraram partições ou dispositivos foram omitidos.");
            var nvme = snapshot.Devices.Single(d => d.Name == "nvme0n1");
            Check(nvme.CapacityBytes == 1073741824 && nvme.Model == "NVMe de teste" && nvme.Rotational == false,
                "Capacidade não usa setores de 512 bytes ou modelo/flags incorretos.");
            var partition = snapshot.Devices.Single(d => d.Name == "nvme0n1p1");
            Check(partition.Parent == "nvme0n1" && partition.CapacityBytes == 524288 && partition.ReadOnly == true,
                "Partição perdeu associação, capacidade ou flag de leitura.");
            var disk = snapshot.Devices.Single(d => d.Name == "sda");
            Check(disk.CapacityBytes == null && disk.Removable == null && disk.Rotational == true && disk.Model == null,
                "Overflow ou dado ausente virou valor válido.");
            Check(snapshot.Devices.Single(d => d.Name == "loop0").CapacityBytes == 0, "Capacidade zero virou indisponível.");
            Check(snapshot.Devices.Single(d => d.Name == "dm-0").CapacityBytes == null, "Capacidade negativa foi aceita.");
            Directory.Delete(Path.Combine(root, "sys/block/nvme0n1"), true);
            Check(!collector.Collect().Devices.Any(d => d.Name.StartsWith("nvme")), "Atualização preservou dispositivo removido.");
            Write("sda/size", "inválido");
            Check(collector.Collect().Devices.Single(d => d.Name == "sda").CapacityBytes == null, "Texto inválido foi aceito.");
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            bool stopped = false;
            try { collector.Collect(cancelled.Token); }
            catch (OperationCanceledException) { stopped = true; }
            Check(stopped, "Cancelamento foi ignorado.");
            Console.WriteLine("OK: inventário de blocos, partições, setores, flags, ausência, overflow, remoção e cancelamento.");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
