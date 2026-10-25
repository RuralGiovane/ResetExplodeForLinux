using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace ResetExplode.Hardware;

public sealed record NetworkDnsResult(string Host, DateTimeOffset StartedAt, DateTimeOffset CompletedAt,
    double ElapsedMilliseconds, IReadOnlyList<string> Addresses)
{
    public string Methodology => NetworkDnsTest.Methodology;
}

public sealed class NetworkDnsTest
{
    public const string Methodology = "Uma resolução de nome pelo sistema, com solicitação de cancelamento após 5 s. Tempo observado inclui cache, hosts, NSS e configuração de VPN/resolvedor; não mede isoladamente um servidor DNS, não identifica qual servidor respondeu e não comprova acesso ao serviço. O encerramento depende da API do sistema.";
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolve;
    private readonly TimeSpan _timeout;

    public NetworkDnsTest() : this((host, token) => Dns.GetHostAddressesAsync(host, token), TimeSpan.FromSeconds(5)) { }
    public NetworkDnsTest(Func<string, CancellationToken, Task<IPAddress[]>> resolve, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(5)) throw new ArgumentOutOfRangeException(nameof(timeout));
        _resolve = resolve;
        _timeout = timeout;
    }

    public static string NormalizeHost(string input)
    {
        string host = input.Trim();
        if (host.Length == 0 || IPAddress.TryParse(host, out _)) throw InvalidHost();
        try { host = new IdnMapping { UseStd3AsciiRules = true }.GetAscii(host).ToLowerInvariant(); }
        catch (ArgumentException) { throw InvalidHost(); }
        string name = host.EndsWith('.') ? host[..^1] : host;
        if (name.Length is 0 or > 253 || IPAddress.TryParse(name, out _) || name.Split('.').Any(label =>
            label.Length is 0 or > 63 || label[0] == '-' || label[^1] == '-' ||
            label.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')))) throw InvalidHost();
        return host;
    }
    private static ArgumentException InvalidHost() => new("Informe somente um nome de domínio/host, sem IP, URL, porta ou caminho.");

    public async Task<NetworkDnsResult> RunAsync(string input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string host = NormalizeHost(input);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);
        var started = DateTimeOffset.UtcNow;
        var elapsed = Stopwatch.StartNew();
        try
        {
            var addresses = await _resolve(host, deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            elapsed.Stop();
            return new(host, started, DateTimeOffset.UtcNow, elapsed.Elapsed.TotalMilliseconds,
                Array.AsReadOnly(addresses.Select(a => a.ToString()).Distinct(StringComparer.Ordinal).ToArray()));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException("Resolução excedeu o prazo de 5 s. Nenhum resultado publicado.");
        }
    }
}
