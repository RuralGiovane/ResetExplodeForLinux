using ResetExplode.Hardware;

internal static class SerialAliasChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        if (!OperatingSystem.IsLinux()) return;
        string root = Path.Combine(Path.GetTempPath(), "reset-alias-" + Guid.NewGuid().ToString("N"));
        string outside = Path.Combine(Path.GetTempPath(), "reset-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "dev/serial/by-id"));
        try
        {
            string aliases = Path.Combine(root, "dev/serial/by-id");
            void Link(string name, string target) => File.CreateSymbolicLink(Path.Combine(aliases, name), target);
            File.WriteAllText(Path.Combine(root, "dev/ttyUSB0"), "Não abrir porta");
            File.WriteAllText(Path.Combine(root, "dev/ttyACM1"), "Não abrir porta");
            File.WriteAllText(Path.Combine(root, "dev/ttyS0"), "Fora do escopo");
            File.WriteAllText(outside, "Fora da raiz");
            var collector = new LinuxSerialCollector(new LinuxFiles(root));
            Check(collector.Collect().Ports.All(p => p.Aliases.Count == 0), "Alias inventado sem links.");
            Link("usb-B", "../../ttyUSB0");
            Link("usb-A com espaço", Path.Combine(root, "dev/ttyUSB0"));
            Link("usb-C", "usb-B");
            Link("usb-acm", "../../ttyACM1");
            Link("quebrado", "../../ttyUSB9");
            Link("circular", "circular");
            Link("outra-porta", "../../ttyS0");
            Link("fora", outside);
            File.WriteAllText(Path.Combine(aliases, "arquivo-regular"), "../../ttyUSB0");
            var first = collector.Collect();
            var usb = first.Ports.Single(p => p.Name == "ttyUSB0");
            Check(usb.Aliases.SequenceEqual(new[] { "/dev/serial/by-id/usb-A com espaço", "/dev/serial/by-id/usb-B", "/dev/serial/by-id/usb-C" }), "Associação/ordem de aliases relativos, absolutos ou encadeados incorreta.");
            Check(first.Ports.Single(p => p.Name == "ttyACM1").Aliases.Count == 1, "Alias ACM foi perdido.");
            Check(first.Notices.Any(n => n.StartsWith("5 entrada(s) by-id")), "Entradas inválidas não foram contabilizadas.");
            Check(!usb.Aliases.Contains(outside), "Destino fora da raiz foi associado.");
            File.Delete(Path.Combine(aliases, "usb-B"));
            Link("usb-B", "../../ttyACM1");
            var changed = collector.Collect();
            Check(changed.Ports.Single(p => p.Name == "ttyACM1").Aliases.Count == 3, "Mudança de destino não atualizou aliases encadeados.");
            Check(usb.Aliases.Count == 3, "Atualização alterou snapshot anterior.");
            File.Delete(Path.Combine(root, "dev/ttyUSB0"));
            Check(collector.Collect().Ports.All(p => p.Name != "ttyUSB0"), "Alias pendente recriou porta removida.");
            Directory.Delete(aliases, true);
            Check(collector.Collect().Ports.All(p => p.Aliases.Count == 0), "Diretório ausente preservou aliases antigos.");
            Console.WriteLine("OK: aliases seriais, links relativos/absolutos/encadeados, inválidos, troca de destino e remoção.");
        }
        finally
        {
            Directory.Delete(root, true);
            File.Delete(outside);
        }
    }
}
