using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ResetExplode.Hardware;

public sealed record NetworkInterfaceConfiguration(string Name, IReadOnlyList<string>? IPv4,
    IReadOnlyList<string>? IPv6, IReadOnlyList<string>? Gateways);

public static class LinuxNetworkAddresses
{
    // Apenas enumeração local. Não resolve nomes, não envia pacotes e não escolhe rota ativa.
    public static IReadOnlyList<NetworkInterfaceConfiguration> Read(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var result = new List<NetworkInterfaceConfiguration>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            cancellation.ThrowIfCancellationRequested();
            IReadOnlyList<string>? v4 = null, v6 = null, gateways = null;
            try
            {
                var properties = nic.GetIPProperties();
                try
                {
                    var addresses = properties.UnicastAddresses.ToArray();
                    string Format(UnicastIPAddressInformation item)
                    {
                        int? prefix = null;
                        try { prefix = item.PrefixLength; }
                        catch (Exception ex) when (IsUnavailable(ex)) { }
                        return FormatAddress(item.Address, prefix);
                    }
                    v4 = Array.AsReadOnly(addresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork).Select(Format).Distinct().ToArray());
                    v6 = Array.AsReadOnly(addresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6).Select(Format).Distinct().ToArray());
                }
                catch (Exception ex) when (IsUnavailable(ex)) { }
                try
                {
                    gateways = Array.AsReadOnly(properties.GatewayAddresses.Select(g => g.Address.ToString()).Distinct().ToArray());
                }
                catch (Exception ex) when (IsUnavailable(ex)) { }
            }
            catch (Exception ex) when (IsUnavailable(ex)) { }
            result.Add(new(nic.Name, v4, v6, gateways));
        }
        cancellation.ThrowIfCancellationRequested();
        return result.AsReadOnly();
    }

    internal static bool IsUnavailable(Exception ex) => ex is NetworkInformationException or SocketException
        or IOException or UnauthorizedAccessException or NotSupportedException;

    internal static string FormatAddress(IPAddress address, int? prefix)
    {
        int maximum = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        return prefix is >= 0 && prefix <= maximum ? $"{address}/{prefix}" : $"{address} (prefixo indisponível)";
    }
}
