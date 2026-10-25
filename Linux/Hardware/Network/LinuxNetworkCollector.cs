using System.Globalization;

namespace ResetExplode.Hardware;

public sealed record NetworkInterfaceEntry(string Name, IReadOnlyList<SystemEntry> Metrics);
public sealed record NetworkInventory(DateTimeOffset Timestamp, IReadOnlyList<NetworkInterfaceEntry> Interfaces,
    IReadOnlyList<string> Notices, ResolverConfiguration Resolver);

/// <summary>Inventário local de sysfs, endereços da API do sistema e resolv.conf. Não testa conectividade.</summary>
public sealed class LinuxNetworkCollector(LinuxFiles files,
    Func<CancellationToken, IReadOnlyList<NetworkInterfaceConfiguration>>? readAddresses = null)
{
    public NetworkInventory Collect(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var notices = new List<string>();
        IReadOnlyList<NetworkInterfaceConfiguration> configurations = [];
        try { configurations = (readAddresses ?? LinuxNetworkAddresses.Read)(cancellation); }
        catch (Exception ex) when (LinuxNetworkAddresses.IsUnavailable(ex))
        { notices.Add("Endereços e gateways indisponíveis nesta coleta; os demais dados de rede continuam disponíveis."); }
        cancellation.ThrowIfCancellationRequested();
        var resolver = LinuxResolverConfiguration.Parse(files.Read("/etc/resolv.conf"));
        if (!resolver.Available) notices.Add("Configuração DNS local indisponível.");
        if (resolver.InvalidEntries > 0) notices.Add($"Configuração DNS: {resolver.InvalidEntries} entrada(s) inválida(s) ignorada(s).");
        var interfaces = new List<NetworkInterfaceEntry>();
        foreach (string path in files.Directories("/sys/class/net", "*"))
        {
            cancellation.ThrowIfCancellationRequested();
            string? Read(string key)
            {
                cancellation.ThrowIfCancellationRequested();
                return files.Read(path + "/" + key);
            }
            ulong? Number(string key) => ulong.TryParse(Read(key), NumberStyles.None,
                CultureInfo.InvariantCulture, out ulong value) ? value : null;
            var metrics = new List<SystemEntry>();
            void Add(string label, string? value) => metrics.Add(new(label, value ?? "Indisponível"));
            string name = Path.GetFileName(path);
            var matching = configurations.Where(c => c.Name == name).ToArray();
            var configuration = matching.Length == 1 ? matching[0] : null;
            string Addresses(IReadOnlyList<string>? values) => values == null ? "Indisponível"
                : values.Count == 0 ? "Nenhum informado" : string.Join(Environment.NewLine, values.Distinct(StringComparer.Ordinal));
            Add("Endereços IPv4", Addresses(configuration?.IPv4));
            Add("Endereços IPv6", Addresses(configuration?.IPv6));
            Add("Gateways reportados", Addresses(configuration?.Gateways));
            Add("Estado operacional", Read("operstate") switch
            {
                "up" => "Ativa", "down" => "Inativa", "unknown" => "Desconhecido",
                "notpresent" => "Não presente", "lowerlayerdown" => "Camada inferior inativa",
                "testing" => "Em teste", "dormant" => "Aguardando ativação", _ => null
            });
            Add("Link reportado", Read("carrier") switch { "1" => "Presente", "0" => "Ausente", _ => null });
            ulong? mtu = Number("mtu");
            Add("MTU", mtu is > 0 and <= uint.MaxValue ? $"{mtu:N0} bytes" : null);
            ulong? speed = Number("speed");
            Add("Velocidade reportada do link", speed is > 0 and < uint.MaxValue ? $"{speed:N0} Mbit/s" : null);
            Add("Duplex", Read("duplex") switch { "full" => "Completo", "half" => "Metade", _ => null });
            foreach (var (key, label) in new[]
            {
                ("rx_bytes", "Bytes recebidos"), ("tx_bytes", "Bytes enviados"),
                ("rx_packets", "Pacotes recebidos"), ("tx_packets", "Pacotes enviados"),
                ("rx_errors", "Erros de recepção"), ("tx_errors", "Erros de transmissão"),
                ("rx_dropped", "Descartes de recepção"), ("tx_dropped", "Descartes de transmissão")
            })
            {
                ulong? value = Number("statistics/" + key);
                Add(label, value?.ToString("N0", CultureInfo.CurrentCulture));
            }
            interfaces.Add(new(Path.GetFileName(path), metrics.AsReadOnly()));
        }
        cancellation.ThrowIfCancellationRequested();
        if (interfaces.Count == 0) notices.Add("Nenhuma interface encontrada ou inventário de rede indisponível.");
        notices.Add("Estado do link não confirma acesso à internet. Contadores são acumulados e podem reiniciar; a velocidade do link não é uma medição de download/upload.");
        notices.Add("Gateways reportados não determinam a rota usada por cada conexão. DNS local pode apontar para um resolvedor intermediário e não descreve DNS específico de VPNs ou aplicativos.");
        return new(DateTimeOffset.Now, interfaces.AsReadOnly(), notices.AsReadOnly(), resolver);
    }
}
