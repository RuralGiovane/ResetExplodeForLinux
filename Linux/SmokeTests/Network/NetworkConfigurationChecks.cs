using System.Net;
using System.Net.NetworkInformation;
using ResetExplode.Hardware;

internal static class NetworkConfigurationChecks
{
    public static void Run()
    {
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
        var unavailable = LinuxResolverConfiguration.Parse(null);
        Check(!unavailable.Available && unavailable.Description == "Indisponível", "DNS ausente apresentado como vazio válido.");
        var empty = LinuxResolverConfiguration.Parse("# sem configuração explícita\nsearch example.test\n");
        Check(empty.Available && empty.Servers.Count == 0 && empty.InvalidEntries == 0, "DNS vazio confundido com erro ou servidor inventado.");
        var dns = LinuxResolverConfiguration.Parse("""
            # nameserver 192.0.2.99
              nameserver 127.0.0.53 # resolvedor local
            nameserver 2001:0db8:0000::53 ; IPv6
            nameserver 192.0.2.53
            nameserver 127.0.0.53
            nameserver 2001:db8::53
            search example.test
            nameserver
            nameserver exemplo.test
            nameserver 127.1
            nameserver 192.0.2.4 extra
            nameserver [::1]
            """);
        Check(dns.Servers.SequenceEqual(new[] { "127.0.0.53", "2001:db8::53", "192.0.2.53" }), "Ordem/deduplicação/IPv6 de DNS incorretos.");
        Check(dns.InvalidEntries == 5, "Entradas DNS inválidas não contabilizadas.");
        Check(LinuxNetworkAddresses.FormatAddress(IPAddress.Parse("192.0.2.10"), 24) == "192.0.2.10/24", "Prefixo IPv4 perdido.");
        Check(LinuxNetworkAddresses.FormatAddress(IPAddress.Parse("fe80::1%7"), 64) == "fe80::1%7/64", "Escopo IPv6 perdido.");
        Check(LinuxNetworkAddresses.FormatAddress(IPAddress.IPv6Loopback, 128) == "::1/128", "Prefixo IPv6 máximo incorreto.");
        Check(LinuxNetworkAddresses.FormatAddress(IPAddress.Any, 0) == "0.0.0.0/0", "Prefixo zero descartado.");
        Check(LinuxNetworkAddresses.FormatAddress(IPAddress.Loopback, 33).Contains("indisponível") &&
            LinuxNetworkAddresses.FormatAddress(IPAddress.IPv6Loopback, -1).Contains("indisponível") &&
            LinuxNetworkAddresses.FormatAddress(IPAddress.Loopback, null).Contains("indisponível"), "Prefixo inválido/ausente aceito.");

        string root = Path.Combine(Path.GetTempPath(), "reset-network-config-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "sys/class/net/eth0"));
            Directory.CreateDirectory(Path.Combine(root, "sys/class/net/lo"));
            Directory.CreateDirectory(Path.Combine(root, "etc"));
            File.WriteAllText(Path.Combine(root, "sys/class/net/eth0/statistics-placeholder"), "");
            File.WriteAllText(Path.Combine(root, "sys/class/net/eth0/mtu"), "1500");
            string config = Path.Combine(root, "etc/resolv.conf");
            File.WriteAllText(config, "nameserver 127.0.0.53\n");
            var ips = new List<string> { "192.0.2.10/24", "192.0.2.11/24" };
            var source = new List<NetworkInterfaceConfiguration>
            {
                new("eth0", ips, ["fe80::1%7/64", "2001:db8::10/64"], ["192.0.2.1"]),
                new("lo", ["127.0.0.1/8"], ["::1/128"], []),
                new("gone", ["192.0.2.90/24"], [], [])
            };
            var files = new LinuxFiles(root);
            var collector = new LinuxNetworkCollector(files, _ => source);
            static string Value(NetworkInventory snapshot, string name, string field) =>
                snapshot.Interfaces.Single(i => i.Name == name).Metrics.Single(m => m.Name == field).Value;
            var first = collector.Collect();
            Check(Value(first, "eth0", "Endereços IPv4") == string.Join(Environment.NewLine, ips), "Endereços múltiplos não apresentados.");
            Check(Value(first, "eth0", "Endereços IPv6").Contains("%7/64"), "Escopo da interface perdido.");
            Check(Value(first, "lo", "Gateways reportados") == "Nenhum informado", "Ausência de gateway interpretada como falha.");
            Check(first.Interfaces.Count == 2 && first.Resolver.Servers.Single() == "127.0.0.53", "Mistura de interfaces ou DNS.");
            ips.Clear(); source.Clear();
            File.WriteAllText(config, "nameserver 192.0.2.53\n");
            var second = collector.Collect();
            Check(Value(second, "eth0", "Endereços IPv4") == "Indisponível", "Interface não associada reutilizou IP antigo.");
            Check(Value(first, "eth0", "Endereços IPv4").Contains("192.0.2.10/24") && first.Resolver.Servers.Single() == "127.0.0.53", "Snapshot anterior alterado.");
            Check(second.Resolver.Servers.Single() == "192.0.2.53", "DNS não atualizado.");
            source.Add(new("eth0", null, [], null));
            var partial = collector.Collect();
            Check(Value(partial, "eth0", "Endereços IPv4") == "Indisponível" && Value(partial, "eth0", "Endereços IPv6") == "Nenhum informado", "Indisponibilidade confundida com lista vazia.");
            source.Add(new("eth0", ["192.0.2.20/24"], [], []));
            Check(Value(collector.Collect(), "eth0", "Endereços IPv4") == "Indisponível", "Associação ambígua aceita.");
            var failed = new LinuxNetworkCollector(files, _ => throw new NetworkInformationException()).Collect();
            Check(failed.Notices.Any(n => n.StartsWith("Endereços e gateways indisponíveis")) &&
                Value(failed, "eth0", "MTU") != "Indisponível" && failed.Resolver.Servers.Count == 1, "Falha de API derrubou sysfs/DNS.");
            bool cancelled = false;
            try { new LinuxNetworkCollector(files, _ => throw new OperationCanceledException()).Collect(); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "Cancelamento da fonte de endereços foi ocultado.");
            File.Delete(config);
            Check(!collector.Collect().Resolver.Available, "Arquivo DNS removido manteve snapshot antigo.");
            Console.WriteLine("OK: IPv4/IPv6, prefixos/escopo, associação, falha parcial, DNS local, parsing, atualização e snapshots.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
