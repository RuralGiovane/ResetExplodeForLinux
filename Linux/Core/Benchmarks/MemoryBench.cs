using System.Diagnostics;

namespace ResetExplode;

public sealed record MemoryResult(double LatencyNs, double ReadGBs, double CopyGBs);

/// <summary>Algoritmos de RAM extraídos de MemoryBench.cs da base Windows. I/O será migrado separadamente.</summary>
public static class MemoryBench
{
    private const long LatencyBytes = 256L << 20;     // 256 MiB: tamanho fixo; pode caber em caches de CPUs futuras
    private const long BandwidthBytes = 1L << 30;     // 1 GiB
    // ------------------------------------------------------------------ RAM

    /// <summary>
    /// Latência: percorre uma lista encadeada em ordem aleatória (permutação de Sattolo = um único ciclo).
    /// Cada leitura depende da anterior, então o processador não consegue adiantar: mede o tempo de ida à RAM.
    /// </summary>
    public static double MeasureLatencyNs(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        int n = (int)(LatencyBytes / sizeof(long));
        var next = new long[n];
        // Saltos de 64 bytes (linhas de cache diferentes) em ordem aleatória determinística.
        int lines = n / 8;
        var order = new int[lines];
        for (int i = 0; i < lines; i++) { if ((i & 0xFFFF) == 0) ct.ThrowIfCancellationRequested(); order[i] = i; }
        var rng = new Random(42);
        for (int i = lines - 1; i > 0; i--) { if ((i & 0xFFFF) == 0) ct.ThrowIfCancellationRequested(); int j = rng.Next(i); (order[i], order[j]) = (order[j], order[i]); }
        for (int i = 0; i < lines; i++) { if ((i & 0xFFFF) == 0) ct.ThrowIfCancellationRequested(); next[order[i] * 8] = order[(i + 1) % lines] * 8; }

        long p = 0;
        for (int i = 0; i < 200_000; i++) { if ((i & 0xFFFF) == 0) ct.ThrowIfCancellationRequested(); p = next[p]; } // aquece TLB/páginas
        const int steps = 20_000_000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < steps; i++)
        {
            p = next[p];
            if ((i & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
        }
        double ns = sw.Elapsed.TotalMilliseconds * 1e6 / steps;
        GC.KeepAlive(p);
        ct.ThrowIfCancellationRequested();
        return ns;
    }

    /// <summary>Banda de leitura (soma) e de cópia com todas as threads, em GB/s (melhor de 3).</summary>
    public static (double Read, double Copy) MeasureBandwidth(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        int threads = Environment.ProcessorCount;
        int n = (int)(BandwidthBytes / sizeof(long));
        var src = new long[n];
        var dst = new long[n];
        var options = new ParallelOptions { CancellationToken = ct };
        Parallel.For(0, threads, options, t =>
        {
            var (start, end) = Partition(n, threads, t);
            for (int offset = start; offset < end; offset += ChunkElements)
            {
                ct.ThrowIfCancellationRequested();
                Array.Fill(src, t + 1L, offset, Math.Min(ChunkElements, end - offset));
            }
        });
        return MeasureBandwidth(src, dst, threads, ct);
    }

    private const int ChunkElements = 65536; // 512 KiB; cancelamento entre blocos.

    internal static (int Start, int End) Partition(int length, int threads, int worker) =>
        ((int)((long)length * worker / threads), (int)((long)length * (worker + 1) / threads));

    internal static long ReadBuffer(long[] src, int threads, CancellationToken ct)
    {
        long total = 0;
        Parallel.For(0, threads, new ParallelOptions { CancellationToken = ct }, t =>
        {
            var (start, end) = Partition(src.Length, threads, t);
            long s0 = 0, s1 = 0, s2 = 0, s3 = 0;
            for (int offset = start; offset < end; offset += ChunkElements)
            {
                ct.ThrowIfCancellationRequested();
                int stop = Math.Min(offset + ChunkElements, end), i = offset;
                for (; i + 3 < stop; i += 4)
                { s0 += src[i]; s1 += src[i + 1]; s2 += src[i + 2]; s3 += src[i + 3]; }
                for (; i < stop; i++) s0 += src[i];
            }
            Interlocked.Add(ref total, s0 + s1 + s2 + s3);
        });
        ct.ThrowIfCancellationRequested();
        return total;
    }

    internal static void CopyBuffer(long[] src, long[] dst, int threads, CancellationToken ct)
    {
        Parallel.For(0, threads, new ParallelOptions { CancellationToken = ct }, t =>
        {
            var (start, end) = Partition(src.Length, threads, t);
            for (int offset = start; offset < end; offset += ChunkElements)
            {
                ct.ThrowIfCancellationRequested();
                Array.Copy(src, offset, dst, offset, Math.Min(ChunkElements, end - offset));
            }
        });
        ct.ThrowIfCancellationRequested();
    }

    internal static (double Read, double Copy) MeasureBandwidth(long[] src, long[] dst,
        int threads, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (src.Length == 0 || dst.Length != src.Length || threads < 1)
            throw new ArgumentException("Buffers não vazios do mesmo tamanho e threads positivas são necessários.");
        double bytes = (long)src.Length * sizeof(long);
        double bestRead = 0, bestCopy = 0;
        for (int rep = 0; rep < 3; rep++)
        {
            var sw = Stopwatch.StartNew();
            long total = ReadBuffer(src, threads, ct);
            double readSeconds = sw.Elapsed.TotalSeconds;
            GC.KeepAlive(total);
            bestRead = Math.Max(bestRead, bytes / readSeconds / 1e9);

            sw.Restart();
            CopyBuffer(src, dst, threads, ct);
            // Tráfego lógico: origem lida + destino escrito; não é tráfego físico do controlador.
            bestCopy = Math.Max(bestCopy, 2.0 * bytes / sw.Elapsed.TotalSeconds / 1e9);
        }
        ct.ThrowIfCancellationRequested();
        return (bestRead, bestCopy);
    }
}
