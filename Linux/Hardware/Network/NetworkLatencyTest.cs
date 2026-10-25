using System.Net;
using System.Net.NetworkInformation;

namespace ResetExplode.Hardware;

public sealed record NetworkProbe(IPStatus Status, long RoundtripMilliseconds);
public sealed record NetworkLatencyResult(string Target, IReadOnlyList<NetworkProbe> Probes)
{
    public int Received => Probes.Count(p => p.Status == IPStatus.Success);
    public double LossPercent => 100d * (Probes.Count - Received) / Probes.Count;
    public double? MinimumMs => Values.Length == 0 ? null : Values.Min();
    public double? AverageMs => Values.Length == 0 ? null : Values.Average();
    public double? MaximumMs => Values.Length == 0 ? null : Values.Max();
    private double[] Values => Probes.Where(p => p.Status == IPStatus.Success).Select(p => (double)p.RoundtripMilliseconds).ToArray();
    public double? JitterMs
    {
        get
        {
            var differences = Probes.Zip(Probes.Skip(1))
                .Where(pair => pair.First.Status == IPStatus.Success && pair.Second.Status == IPStatus.Success)
                .Select(pair => Math.Abs((double)pair.Second.RoundtripMilliseconds - pair.First.RoundtripMilliseconds)).ToArray();
            return differences.Length == 0 ? null : differences.Average();
        }
    }
}

public sealed class NetworkLatencyTest
{
    private readonly Func<IPAddress, CancellationToken, Task<NetworkProbe>> _probe;
    private readonly TimeSpan _interval;
    public NetworkLatencyTest() : this(SendAsync, TimeSpan.FromMilliseconds(500)) { }
    public NetworkLatencyTest(Func<IPAddress, CancellationToken, Task<NetworkProbe>> probe, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(probe);
        if (interval < TimeSpan.Zero || interval > TimeSpan.FromSeconds(5)) throw new ArgumentOutOfRangeException(nameof(interval));
        _probe = probe;
        _interval = interval;
    }

    public async Task<NetworkLatencyResult> RunAsync(string target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IPAddress.TryParse(target.Trim(), out var address) ||
            address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.Broadcast) || address.IsIPv6Multicast ||
            (address.GetAddressBytes().Length == 4 && address.GetAddressBytes()[0] is >= 224 and <= 239) ||
            address.IsIPv4MappedToIPv6)
            throw new ArgumentException("Informe um endereço IPv4 ou IPv6 unicast, sem URL ou nome de domínio.");
        var probes = new List<NetworkProbe>();
        for (int i = 0; i < 10; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            probes.Add(await _probe(address, cancellationToken).ConfigureAwait(false));
            cancellationToken.ThrowIfCancellationRequested();
            if (i < 9) await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
        }
        return new NetworkLatencyResult(address.ToString(), probes.AsReadOnly());
    }

    private static async Task<NetworkProbe> SendAsync(IPAddress address, CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(address, TimeSpan.FromSeconds(2), cancellationToken: cancellationToken).ConfigureAwait(false);
        return new NetworkProbe(reply.Status, reply.RoundtripTime);
    }
}
