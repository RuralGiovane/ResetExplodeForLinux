using ResetExplode.Hardware;

internal static class MountChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "reset-mounts-" + Guid.NewGuid().ToString("N"));
        try
        {
            var escaped = LinuxMountCollector.ParseLine(@"24 1 8:1 /sub\040vol /media/Meu\040Disco rw shared:1 - ext4 /dev/sda1 rw");
            Check(escaped?.MountPoint == "/media/Meu Disco" && escaped.Root == "/sub vol" && !escaped.ReadOnly, "Parsing de campos opcionais/escapes falhou.");
            var slash = LinuxMountCollector.ParseLine(@"25 1 8:1 / /a\134040 rw - ext4 /dev/sda1 ro");
            Check(slash?.MountPoint == @"/a\040" && slash.ReadOnly, "Escape foi decodificado duas vezes ou ro do sistema foi perdido.");
            Check(LinuxMountCollector.ParseLine("inválido") == null, "Linha inválida foi aceita.");
            Check(LinuxMountCollector.ParseLine("1 2 x:y / / rw - ext4 /dev/sda rw") == null, "Número de dispositivo inválido aceito.");
            var files = new LinuxFiles(root);
            int calls = 0;
            FileSystemSpace Read(string point)
            {
                calls++;
                if (point == "/denied") throw new UnauthorizedAccessException("simulado");
                if (point == "/invalid") return new(10, 20, 0);
                return new(1000, 400, 300);
            }
            var collector = new LinuxMountCollector(files, Read);
            Check(collector.Collect().Notices.Count > 0 && calls == 0, "Ausência de mountinfo não foi tratada.");
            Directory.CreateDirectory(Path.Combine(root, "proc/self"));
            string path = Path.Combine(root, "proc/self/mountinfo");
            File.WriteAllText(path, """
                1 9 8:1 / / rw - ext4 /dev/sda1 rw
                2 9 0:20 / /network rw - nfs host:/export rw
                3 9 0:21 / /proc rw - proc proc rw
                4 9 8:2 / /denied rw - xfs /dev/sdb rw
                5 9 8:3 / /invalid rw - ext4 /dev/sdc rw
                6 9 8:4 / /same rw - ext4 /dev/sdd rw
                7 9 8:5 / /same rw - ext4 /dev/sde rw
                inválido
                """);
            var result = collector.Collect();
            Check(result.Mounts.Count == 7 && calls == 3 && result.Notices.Count == 1, "Filtragem de consultas ou linhas inválidas falhou.");
            var space = result.Mounts.Single(m => m.MountPoint == "/").Space;
            Check(space?.UsedBytes == 600 && space.AvailableBytes == 300 && space.FreeBytes == 400, "Disponível foi confundido com livre total.");
            Check(result.Mounts.Where(m => m.MountPoint != "/").All(m => m.Space == null && m.Notice != null), "Falha ou consulta omitida perdeu aviso.");
            File.WriteAllText(path, "1 9 8:1 / / rw - ext4 /dev/sda1 rw\n");
            Check(collector.Collect().Mounts.Count == 1, "Montagens removidas permaneceram no inventário.");
            Check(new FileSystemSpace(0, 0, 0).IsValid && !new FileSystemSpace(10, 5, 6).IsValid, "Validação de contadores incorreta.");
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            bool stopped = false;
            try { collector.Collect(cancelled.Token); }
            catch (OperationCanceledException) { stopped = true; }
            Check(stopped, "Cancelamento foi ignorado.");
            Console.WriteLine("OK: montagens, escapes, espaço livre/disponível, sobreposição, falhas e cancelamento.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
