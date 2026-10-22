using ResetExplode.Hardware;

internal static class SerialChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        SerialUsbChecks.Run();
        SerialAliasChecks.Run();
        string root = Path.Combine(Path.GetTempPath(), "reset-serial-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            void Write(string path, string value)
            {
                string target = Path.Combine(root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, value);
            }
            var collector = new LinuxSerialCollector(new LinuxFiles(root));
            var absent = collector.Collect();
            Check(absent.Ports.Count == 0 && absent.Groups.All(g => g.Active == null), "Ausência virou acesso confirmado.");
            Write("sys/class/tty/ttyUSB0/dev", "188:0");
            Write("sys/class/tty/ttyACM1/dev", "166:1");
            Write("sys/class/tty/ttyUSB/dev", "188:2");
            Write("sys/class/tty/ttyUSBx/dev", "188:3");
            Write("sys/class/tty/ttyS0/dev", "4:64");
            Write("dev/ttyUSB0", "NÃO LER A PORTA");
            Write("dev/ttyACM2", "NÃO LER A PORTA");
            Write("etc/group", "dialout:x:20:usuario\nuucp:x:14:\n");
            Write("proc/self/status", "Gid:\t1000\t14\t1000\t14\nGroups:\t20 1000\n");
            var first = collector.Collect();
            Check(first.Ports.Count == 3, "Portas omitidas, duplicadas ou nomes não seriais aceitos.");
            var usb = first.Ports.Single(p => p.Name == "ttyUSB0");
            Check(usb.ListedInDev && usb.ListedInSysfs && usb.DeviceNumber == "188:0", "Fontes da porta foram perdidas.");
            Check(!first.Ports.Single(p => p.Name == "ttyACM1").ListedInDev, "sysfs confundido com presença em /dev.");
            Check(!first.Ports.Single(p => p.Name == "ttyACM2").ListedInSysfs, "/dev confundido com sysfs.");
            Check(first.Groups.All(g => g.Active == true), "Grupos efetivo/suplementar ignorados.");
            Write("proc/self/status", "Gid: 20 1000 20 1000\nGroups:\n");
            Check(collector.Collect().Groups.All(g => g.Active == false), "GID real/salvo ou /etc/group virou grupo ativo.");
            Write("proc/self/status", "Gid: 1000 1000 1000 1000\nGroups: inválido 20\n");
            Check(collector.Collect().Groups.All(g => g.Active == null), "Grupos malformados viraram certeza.");
            Write("etc/group", "dialout:x:20:\ndialout:x:21:\nuucp:x:inválido:\n");
            Check(collector.Collect().Groups.All(g => g.Id == null), "GIDs ambíguos/inválidos foram aceitos.");
            Write("sys/class/tty/ttyUSB0/dev", "-1:inválido");
            Check(collector.Collect().Ports.Single(p => p.Name == "ttyUSB0").DeviceNumber == null, "major:minor inválido aceito.");
            Directory.Delete(Path.Combine(root, "sys/class/tty/ttyUSB0"), true);
            File.Delete(Path.Combine(root, "dev/ttyUSB0"));
            Check(!collector.Collect().Ports.Any(p => p.Name == "ttyUSB0") && first.Ports.Count == 3, "Remoção ou snapshot independente falhou.");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            bool stopped = false;
            try { collector.Collect(cancellation.Token); }
            catch (OperationCanceledException) { stopped = true; }
            Check(stopped, "Cancelamento serial ignorado.");
            Console.WriteLine("OK: portas seriais, fontes distintas, grupos ativos, ausência, dados inválidos, remoção e cancelamento.");
        }
        finally { Directory.Delete(root, true); }
    }
}
