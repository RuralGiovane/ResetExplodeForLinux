using System.Globalization;
using ResetExplode.Hardware;

internal static class NetworkChecks
{
    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "reset-network-" + Guid.NewGuid().ToString("N"));
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            void Write(string path, string value)
            {
                string target = Path.Combine(root, "sys/class/net", path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, value);
            }
            static string Value(NetworkInventory inventory, string name, string metric) =>
                inventory.Interfaces.Single(i => i.Name == name).Metrics.Single(m => m.Name == metric).Value;
            static void Check(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
            }
            var collector = new LinuxNetworkCollector(new LinuxFiles(root), _ => []);
            var empty = collector.Collect();
            Check(empty.Interfaces.Count == 0 && empty.Notices.Count > 0, "Rede ausente não foi informada.");
            Write("eth0/operstate", "up"); Write("eth0/carrier", "1");
            Write("eth0/mtu", "1500\n"); Write("eth0/speed", "1000"); Write("eth0/duplex", "full");
            Write("eth0/statistics/rx_bytes", "18446744073709551615");
            Write("eth0/statistics/tx_bytes", "0"); Write("eth0/statistics/rx_errors", "0");
            Write("eth0/statistics/tx_errors", "-1"); Write("eth0/statistics/rx_dropped", "18446744073709551616");
            Write("lo/operstate", "unknown"); Write("lo/carrier", "1"); Write("lo/speed", "-1");
            Write("wlan0/operstate", "down"); Write("wlan0/carrier", "0"); Write("wlan0/mtu", "0");
            var first = collector.Collect();
            Check(first.Interfaces.Select(i => i.Name).SequenceEqual(new[] { "eth0", "lo", "wlan0" }), "Interfaces não ordenadas ou omitidas.");
            Check(Value(first, "eth0", "Estado operacional") == "Ativa" && Value(first, "eth0", "Link reportado") == "Presente", "Estado/link incorretos.");
            Check(Value(first, "eth0", "MTU") == "1.500 bytes" && Value(first, "eth0", "Velocidade reportada do link") == "1.000 Mbit/s", "Unidades/cultura incorretas.");
            Check(Value(first, "eth0", "Bytes recebidos") == ulong.MaxValue.ToString("N0") && Value(first, "eth0", "Bytes enviados") == "0", "Contador uint64 ou zero perdido.");
            Check(Value(first, "eth0", "Erros de transmissão") == "Indisponível" && Value(first, "eth0", "Descartes de recepção") == "Indisponível", "Negativo ou overflow aceito.");
            Check(Value(first, "eth0", "Pacotes enviados") == "Indisponível", "Ausência virou zero.");
            Check(Value(first, "lo", "Estado operacional") == "Desconhecido" && Value(first, "lo", "Velocidade reportada do link") == "Indisponível", "Loopback desconhecido ganhou diagnóstico falso.");
            Check(Value(first, "wlan0", "Link reportado") == "Ausente" && Value(first, "wlan0", "MTU") == "Indisponível", "Link ausente/MTU inválido incorretos.");
            Write("eth0/statistics/rx_bytes", "4"); Write("eth0/operstate", "lowerlayerdown");
            Write("eth0/speed", "4294967295"); Write("eth0/carrier", "2");
            var changed = collector.Collect();
            Check(Value(changed, "eth0", "Bytes recebidos") == "4", "Contador reiniciado não foi atualizado.");
            Check(Value(changed, "eth0", "Estado operacional") == "Camada inferior inativa", "Estado inferior não apresentado.");
            Check(Value(changed, "eth0", "Velocidade reportada do link") == "Indisponível" && Value(changed, "eth0", "Link reportado") == "Indisponível", "Sentinela/flag inválida aceita.");
            Check(Value(first, "eth0", "Bytes recebidos") == ulong.MaxValue.ToString("N0"), "Snapshot anterior modificado.");
            Directory.Delete(Path.Combine(root, "sys/class/net/eth0"), true);
            Check(!collector.Collect().Interfaces.Any(i => i.Name == "eth0"), "Interface removida permaneceu.");
            Write("eth0/operstate", "dormant");
            Check(Value(collector.Collect(), "eth0", "Bytes recebidos") == "Indisponível", "Reaparecimento reutilizou contadores antigos.");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            bool stopped = false;
            try { collector.Collect(cancelled.Token); } catch (OperationCanceledException) { stopped = true; }
            Check(stopped, "Coleta ignorou cancelamento.");
            Console.WriteLine("OK: rede, estados, unidades, uint64, zero/ausência, valores inválidos, snapshots, remoção e cancelamento.");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
