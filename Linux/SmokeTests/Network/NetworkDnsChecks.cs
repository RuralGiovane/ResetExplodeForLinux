using System.Net;
using System.Net.Sockets;
using ResetExplode.Hardware;

namespace ResetExplode;

internal static class NetworkDnsChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static async Task Fails<T>(Func<Task> work) where T : Exception
    {
        try { await work(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Esperava {typeof(T).Name}, não resultado.");
    }
    public static async Task RunAsync()
    {
        int calls = 0;
        var source = new[] { IPAddress.Parse("192.0.2.1"), IPAddress.Parse("fe80::1%7"), IPAddress.Parse("192.0.2.1") };
        var test = new NetworkDnsTest((host, ct) =>
        {
            calls++;
            Check(host == "example.com.", "Nome normalizado incorreto.");
            return Task.FromResult(source);
        }, TimeSpan.FromSeconds(5));
        var result = await test.RunAsync(" Example.COM. ");
        source[0] = IPAddress.Loopback;
        Check(result.Addresses.SequenceEqual(new[] { "192.0.2.1", "fe80::1%7" }) && result.CompletedAt >= result.StartedAt && double.IsFinite(result.ElapsedMilliseconds) && result.ElapsedMilliseconds >= 0, "Snapshot, escopo, ordem, datas ou duração incorretos.");
        Check(NetworkDnsTest.NormalizeHost("bücher.example") == "xn--bcher-kva.example" && NetworkDnsTest.NormalizeHost("localhost") == "localhost", "IDN/host local rejeitado.");
        foreach (string input in new[] { "", ".", "https://example.com", "example.com/path", "example.com:53", "a..com", "-a.com", "a-.com", "a_b.com", "127.0.0.1", "127.0.0.1.", "::1", "a b.com", "<script>", new string('a', 64) + ".com" })
            await Fails<ArgumentException>(() => test.RunAsync(input));
        Check(calls == 1, "Entrada inválida consultou resolvedor.");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            await Fails<OperationCanceledException>(() => test.RunAsync("example.com", cancelled.Token));
            Check(calls == 1, "Pré-cancelamento consultou resolvedor.");
        }
        using (var cancelled = new CancellationTokenSource())
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var blocking = new NetworkDnsTest(async (host, ct) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return [];
            }, TimeSpan.FromSeconds(5));
            var work = blocking.RunAsync("example.com", cancelled.Token);
            await started.Task;
            cancelled.Cancel();
            await Fails<OperationCanceledException>(() => work.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        var timeout = new NetworkDnsTest(async (host, ct) => { await Task.Delay(Timeout.Infinite, ct); return []; }, TimeSpan.FromMilliseconds(20));
        // TimeoutException precisa vir do serviço, não de WaitAsync.
        var workTimeout = timeout.RunAsync("example.com");
        await Fails<TimeoutException>(() => workTimeout);
        Check(workTimeout.IsCompleted, "Timeout não concluiu a tarefa.");
        var missing = new NetworkDnsTest((host, ct) => throw new SocketException((int)SocketError.HostNotFound), TimeSpan.FromSeconds(5));
        await Fails<SocketException>(() => missing.RunAsync("example.invalid"));
        var empty = await new NetworkDnsTest((host, ct) => Task.FromResult(Array.Empty<IPAddress>()), TimeSpan.FromSeconds(5)).RunAsync("empty.example");
        Check(empty.Addresses.Count == 0, "Resposta vazia fabricou endereços.");
        using (var cancelled = new CancellationTokenSource())
        {
            var late = new NetworkDnsTest((host, ct) => { cancelled.Cancel(); return Task.FromResult(new[] { IPAddress.Loopback }); }, TimeSpan.FromSeconds(5));
            await Fails<OperationCanceledException>(() => late.RunAsync("localhost", cancelled.Token));
        }
        Console.WriteLine("OK: resolução simulada, validação/IDN, IPv4/IPv6, snapshot, ausência, falha, timeout e cancelamento; sem consultas externas.");
    }
}
