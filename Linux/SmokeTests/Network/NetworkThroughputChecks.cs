using System.Net;
using ResetExplode.Hardware;

namespace ResetExplode;

internal static class NetworkThroughputChecks
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return send(request, token);
        }
    }

    private sealed class WaitingStream : Stream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static async Task Fails<T>(Func<Task> work) where T : Exception
    {
        try { await work(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Esperava falha {typeof(T).Name}, não resultado parcial.");
    }
    private static HttpResponseMessage Download() => new(HttpStatusCode.OK)
    { Content = new ByteArrayContent(new byte[NetworkThroughputTest.DownloadBytes]) };

    public static async Task RunAsync()
    {
        int stage = 0;
        using var handler = new Handler(async (request, ct) =>
        {
            Check(request.RequestUri!.Host == "speed.cloudflare.com" && request.RequestUri.Scheme == "https", "Destino errado.");
            Check(request.Headers.CacheControl!.NoCache && request.Headers.AcceptEncoding.Single().Value == "identity", "Cache/compressão não controlados.");
            if (stage++ == 0)
            {
                Check(request.Method == HttpMethod.Get && request.RequestUri.Query == "?bytes=10000000", "Download incorreto.");
                return Download();
            }
            Check(request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath == "/__up" && request.Content!.Headers.ContentLength == 5_000_000, "Upload incorreto.");
            await request.Content!.CopyToAsync(Stream.Null, ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
        });
        using var http = new HttpClient(handler);
        var result = await new NetworkThroughputTest(http).RunAsync();
        Check(handler.Calls == 2 && result.Download.Bytes == 10_000_000 && result.Upload.Bytes == 5_000_000 && result.CompletedAt >= result.StartedAt, "Ordem/contagem/datas erradas.");
        Check(double.IsFinite(result.Download.MegabitsPerSecond) && result.Download.MegabitsPerSecond > 0 && result.Upload.MegabitsPerSecond > 0, "Taxa inválida.");
        Check(new NetworkTransferMeasurement(10_000_000, 2.5).MegabitsPerSecond == 32, "Conversão para Mbit/s errada.");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            await Fails<OperationCanceledException>(() => new NetworkThroughputTest(http).RunAsync(cancelled.Token));
            Check(handler.Calls == 2, "Pré-cancelamento fez requisição.");
        }
        foreach (var mode in new[] { "status", "redirect", "short", "truncated", "encoding", "upload-failure", "early-upload" })
        {
            int calls = 0;
            using var failureHandler = new Handler((request, ct) =>
            {
                calls++;
                if (mode == "status") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                if (mode == "redirect") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect));
                if (mode == "short") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) });
                if (mode == "truncated")
                {
                    var shortResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(new byte[32])) };
                    shortResponse.Content.Headers.ContentLength = NetworkThroughputTest.DownloadBytes;
                    return Task.FromResult(shortResponse);
                }
                if (calls == 2) return Task.FromResult(new HttpResponseMessage(mode == "upload-failure" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
                var response = Download();
                if (mode == "encoding") response.Content.Headers.ContentEncoding.Add("gzip");
                return Task.FromResult(response);
            });
            using var failureHttp = new HttpClient(failureHandler);
            if (mode is "status" or "redirect" or "upload-failure") await Fails<HttpRequestException>(() => new NetworkThroughputTest(failureHttp).RunAsync());
            else await Fails<IOException>(() => new NetworkThroughputTest(failureHttp).RunAsync());
            Check(calls == (mode is "upload-failure" or "early-upload" ? 2 : 1), "Falha causou retry ou próxima etapa.");
        }
        using (var waiting = new WaitingStream())
        using (var blocking = new Handler((request, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(waiting) })))
        using (var blockingHttp = new HttpClient(blocking))
        using (var cancellation = new CancellationTokenSource())
        {
            var work = new NetworkThroughputTest(blockingHttp).RunAsync(cancellation.Token);
            await waiting.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Fails<OperationCanceledException>(() => work.WaitAsync(TimeSpan.FromSeconds(2)));
            Check(waiting.Disposed && blocking.Calls == 1, "Cancelamento não liberou stream ou iniciou upload.");
        }
        using (var waiting = new WaitingStream())
        using (var blocking = new Handler((request, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(waiting) })))
        using (var blockingHttp = new HttpClient(blocking))
        {
            await Fails<TimeoutException>(() => new NetworkThroughputTest(blockingHttp, TimeSpan.FromMilliseconds(50)).RunAsync().WaitAsync(TimeSpan.FromSeconds(3)));
            Check(waiting.Disposed && blocking.Calls == 1, "Timeout não interrompeu transferência.");
        }
        using (var cancellation = new CancellationTokenSource())
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var uploadHandler = new Handler(async (request, ct) =>
            {
                if (request.Method == HttpMethod.Get) return Download();
                started.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
            using var uploadHttp = new HttpClient(uploadHandler);
            var work = new NetworkThroughputTest(uploadHttp).RunAsync(cancellation.Token);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Fails<OperationCanceledException>(() => work.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        Console.WriteLine("OK: throughput HTTP simulado, limites, bytes, ordem, conversão, erros, resposta antecipada, cancelamento e timeout; sem tráfego real.");
    }
}
