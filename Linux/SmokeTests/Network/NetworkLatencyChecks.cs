using System.Net.NetworkInformation;
using ResetExplode.Hardware;

namespace ResetExplode;

internal static class NetworkLatencyChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        var samples = new Queue<NetworkProbe>(new[] {
            new NetworkProbe(IPStatus.Success, 10), new NetworkProbe(IPStatus.Success, 30),
            new NetworkProbe(IPStatus.TimedOut, 0), new NetworkProbe(IPStatus.Success, 100),
            new NetworkProbe(IPStatus.Success, 80), new NetworkProbe(IPStatus.DestinationHostUnreachable, 0),
            new NetworkProbe(IPStatus.TimedOut, 0), new NetworkProbe(IPStatus.TimedOut, 0),
            new NetworkProbe(IPStatus.TimedOut, 0), new NetworkProbe(IPStatus.Success, 0) });
        var test = new NetworkLatencyTest((ip, ct) => Task.FromResult(samples.Dequeue()), TimeSpan.Zero);
        var result = await test.RunAsync("127.0.0.1");
        Check(result.Received == 5 && result.LossPercent == 50, "Contagem/perda incorreta.");
        Check(result.MinimumMs == 0 && result.AverageMs == 44 && result.MaximumMs == 100, "RTT incorreto ou zero descartado.");
        Check(result.JitterMs == 20, "Jitter atravessou tentativas perdidas.");
        var missing = new NetworkLatencyTest((ip, ct) => Task.FromResult(new NetworkProbe(IPStatus.TimedOut, 0)), TimeSpan.Zero);
        var none = await missing.RunAsync("::1");
        Check(none.LossPercent == 100 && none.AverageMs == null && none.JitterMs == null, "Sem respostas deve deixar tempos indisponíveis.");
        int calls = 0;
        var success = new NetworkLatencyTest((ip, ct) => { calls++; return Task.FromResult(new NetworkProbe(IPStatus.Success, 0)); }, TimeSpan.Zero);
        foreach (var target in new[] { "", "https://example.com", "example.com", "0.0.0.0", "::", "224.0.0.1", "ff02::1", "255.255.255.255", "::ffff:224.0.0.1" })
        {
            try { await success.RunAsync(target); throw new InvalidOperationException("Destino inválido aceito."); }
            catch (ArgumentException) { }
        }
        Check(calls == 0, "Destino inválido enviou pacote.");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try { await success.RunAsync("127.0.0.1", cancelled.Token); throw new InvalidOperationException("Pré-cancelamento ignorado."); }
            catch (OperationCanceledException) { }
            Check(calls == 0, "Pré-cancelamento enviou pacote.");
        }
        using (var cancelled = new CancellationTokenSource())
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var blocking = new NetworkLatencyTest(async (ip, ct) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return new NetworkProbe(IPStatus.Success, 1);
            }, TimeSpan.Zero);
            var work = blocking.RunAsync("127.0.0.1", cancelled.Token);
            await started.Task;
            cancelled.Cancel();
            try { await work.WaitAsync(TimeSpan.FromSeconds(2)); throw new InvalidOperationException("Cancelamento retornou resultado parcial."); }
            catch (OperationCanceledException) { }
        }
        using (var cancelled = new CancellationTokenSource())
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var delay = new NetworkLatencyTest((ip, ct) =>
            {
                started.TrySetResult();
                return Task.FromResult(new NetworkProbe(IPStatus.Success, 1));
            }, TimeSpan.FromSeconds(5));
            var work = delay.RunAsync("127.0.0.1", cancelled.Token);
            await started.Task;
            cancelled.Cancel();
            try { await work.WaitAsync(TimeSpan.FromSeconds(2)); throw new InvalidOperationException("Pausa não cancelou."); }
            catch (OperationCanceledException) { }
        }
        var failure = new NetworkLatencyTest((ip, ct) => throw new PingException("Permissão negada simulada"), TimeSpan.Zero);
        try { await failure.RunAsync("127.0.0.1"); throw new InvalidOperationException("Erro local convertido em perda."); }
        catch (PingException) { }
        var all = await success.RunAsync("::1");
        Check(all.Received == 10 && all.LossPercent == 0 && all.JitterMs == 0, "Respostas constantes inválidas.");
        Check(result.Probes.Count == 10 && result.AverageMs == 44, "Snapshot anterior alterado.");
        Console.WriteLine("OK: latência, jitter, perda, destinos, erros e cancelamento de rede (sem tráfego real).");
    }
}
