using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace ResetExplode.Hardware;

public sealed record NetworkTransferMeasurement(long Bytes, double Seconds)
{
    public double MegabitsPerSecond => Bytes * 8d / 1_000_000 / Seconds;
}

public sealed record NetworkThroughputResult(DateTimeOffset StartedAt, DateTimeOffset CompletedAt,
    NetworkTransferMeasurement Download, NetworkTransferMeasurement Upload)
{
    public string Server => "https://speed.cloudflare.com";
    public string Methodology => NetworkThroughputTest.Methodology;
}

/// <summary>Transferências HTTP limitadas e sequenciais; nenhum resultado parcial é publicado.</summary>
public sealed class NetworkThroughputTest
{
    public const int DownloadBytes = 10_000_000;
    public const int UploadBytes = 5_000_000;
    public const string Methodology = "HTTP sequencial: 10 MB de download e 5 MB de upload, sem aquecimento ou paralelismo; até 20 s por etapa. Mbit/s = bytes do corpo × 8 / segundos / 1.000.000. Tempo inclui conexão, TLS e espera do servidor; upload conta somente corpo enviado com resposta HTTP de sucesso. Taxa pontual, não capacidade máxima da conexão. Tráfego de protocolo e respostas é adicional. Erro, timeout ou cancelamento descarta os dois resultados.";
    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;

    public NetworkThroughputTest(HttpClient http, TimeSpan? timeout = null)
    {
        _http = http;
        _timeout = timeout ?? TimeSpan.FromSeconds(20);
        if (_timeout <= TimeSpan.Zero || _timeout > TimeSpan.FromSeconds(20)) throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public static HttpClient CreateClient() => new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.None,
        UseCookies = false, ConnectTimeout = TimeSpan.FromSeconds(10), MaxResponseHeadersLength = 16
    }) { Timeout = Timeout.InfiniteTimeSpan };

    public async Task<NetworkThroughputResult> RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var started = DateTimeOffset.UtcNow;
        // Dados sintéticos; nenhum arquivo ou dado da máquina é enviado.
        byte[] payload = RandomNumberGenerator.GetBytes(UploadBytes);
        var download = await MeasureAsync(false, payload, cancellationToken).ConfigureAwait(false);
        var upload = await MeasureAsync(true, payload, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return new(started, DateTimeOffset.UtcNow, download, upload);
    }

    private async Task<NetworkTransferMeasurement> MeasureAsync(bool upload, byte[] payload, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);
        var token = deadline.Token;
        string phase = upload ? "Upload" : "Download";
        using var request = new HttpRequestMessage(upload ? HttpMethod.Post : HttpMethod.Get,
            upload ? "https://speed.cloudflare.com/__up" : $"https://speed.cloudflare.com/__down?bytes={DownloadBytes}");
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
        request.Headers.AcceptEncoding.ParseAdd("identity");
        if (upload)
        {
            request.Content = new UploadContent(payload);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        }
        var elapsed = Stopwatch.StartNew();
        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (upload && request.Content is UploadContent content && !content.FullyWritten)
                throw new IOException("Upload não foi enviado integralmente antes da resposta.");
            if (response.Content.Headers.ContentEncoding.Any(e => !e.Equals("identity", StringComparison.OrdinalIgnoreCase)))
                throw new IOException($"{phase}: resposta comprimida inesperada.");
            long limit = upload ? 4096 : DownloadBytes;
            if (response.Content.Headers.ContentLength is { } length && (upload ? length > limit : length != limit))
                throw new IOException($"{phase}: tamanho de resposta inesperado.");
            using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            byte[] buffer = new byte[65536];
            long received = 0;
            while (received < limit)
            {
                int count = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, limit - received)), token).ConfigureAwait(false);
                if (count == 0) break;
                received += count;
            }
            if (!upload && received != DownloadBytes) throw new IOException("Download incompleto.");
            // O corpo de resposta de upload é ignorado e limitado; a confirmação é o status HTTP.
            token.ThrowIfCancellationRequested();
            elapsed.Stop();
            return new(upload ? UploadBytes : received, Math.Max(elapsed.Elapsed.TotalSeconds, 0.000001));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException($"{phase}: limite de tempo excedido. Nenhum resultado parcial publicado.");
        }
    }
    private sealed class UploadContent(byte[] payload) : HttpContent
    {
        public bool FullyWritten { get; private set; }
        protected override bool TryComputeLength(out long length) { length = payload.Length; return true; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            FullyWritten = true;
        }
    }
}
