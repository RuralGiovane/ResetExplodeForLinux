using System.Diagnostics;

namespace ResetExplode;

public sealed record DiskProgress(string Phase, double Fraction);

internal sealed record DiskWorkload(long FileBytes, int AssetCount, int AssetBytes, TimeSpan RandomDuration)
{
    public static DiskWorkload Default { get; } = new(512L << 20, 1000, 64 << 10, TimeSpan.FromSeconds(4));
}

public static class DiskBenchmarks
{
    private const int Block = 1 << 20, Small = 4096;
    private static IDirectFile Open(string path, bool create) => new LinuxDirectFile(path, create);
    public static BenchmarkMeasurement RunDisk(string parent, Action<DiskProgress> progress, CancellationToken ct) =>
        RunDisk(parent, progress, ct, DiskWorkload.Default, Open);
    public static BenchmarkMeasurement RunLoading(string parent, Action<DiskProgress> progress, CancellationToken ct) =>
        RunLoading(parent, progress, ct, DiskWorkload.Default, Open);

    internal static BenchmarkMeasurement RunDisk(string parent, Action<DiskProgress> progress, CancellationToken ct,
        DiskWorkload workload, Func<string, bool, IDirectFile> open)
    {
        ct.ThrowIfCancellationRequested();
        using var workspace = new BenchmarkWorkspace(parent, workload.FileBytes + (512L << 20));
        using var buffer = new AlignedBuffer(Block);
        new Random(42).NextBytes(buffer.Span);
        using var file = open(workspace.FilePath("disk.bin"), true);
        double Sequential(bool write, double start)
        {
            progress(new(write ? "Escrita sequencial" : "Leitura sequencial", start));
            var sw = Stopwatch.StartNew();
            for (long offset = 0; offset < workload.FileBytes; offset += Block)
            {
                if (write) file.Write(buffer, Block, offset, ct); else file.Read(buffer, Block, offset, ct);
                progress(new(write ? "Escrita sequencial" : "Leitura sequencial", start + 0.25 * (offset + Block) / workload.FileBytes));
            }
            ct.ThrowIfCancellationRequested();
            return workload.FileBytes / 1048576.0 / sw.Elapsed.TotalSeconds;
        }
        double writeRate = Sequential(true, 0), readRate = Sequential(false, .25);
        var read = RandomSingle(file, buffer, workload, false, ct, progress, .5);
        var write = RandomSingle(file, buffer, workload, true, ct, progress, .65);
        progress(new("Leitura 4 KiB · até 8 workers", .8));
        long operations = 0;
        var timer = Stopwatch.StartNew();
        Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, worker =>
        {
            using var local = new AlignedBuffer(Small);
            var random = new Random(100 + worker);
            long count = 0;
            do
            {
                file.Read(local, Small, random.NextInt64(workload.FileBytes / Small) * Small, ct);
                count++;
            } while (timer.Elapsed < workload.RandomDuration);
            Interlocked.Add(ref operations, count);
        });
        double parallelIops = operations / timer.Elapsed.TotalSeconds;
        ct.ThrowIfCancellationRequested();
        progress(new("Concluído; limpando arquivos", 1));
        return new($"Disco: leitura {readRate:N1} MiB/s · escrita {writeRate:N1} MiB/s · 4 KiB leitura {read.Iops:N0} / escrita {write.Iops:N0} IOPS · até 8 workers {parallelIops:N0} IOPS", null,
            new() { ["sequentialReadMiBs"] = readRate, ["sequentialWriteMiBs"] = writeRate,
                ["randomReadIops"] = read.Iops, ["randomWriteIops"] = write.Iops,
                ["randomReadMeanUs"] = read.MeanUs, ["randomWriteMeanUs"] = write.MeanUs,
                ["randomRead8WorkersIops"] = parallelIops, ["fileBytes"] = workload.FileBytes });
    }

    private static (double Iops, double MeanUs) RandomSingle(IDirectFile file, AlignedBuffer buffer,
        DiskWorkload workload, bool write, CancellationToken ct, Action<DiskProgress> progress, double start)
    {
        string phase = write ? "Escrita aleatória 4 KiB · 1 por vez" : "Leitura aleatória 4 KiB · 1 por vez";
        progress(new(phase, start));
        var random = new Random(write ? 7 : 3);
        long operations = 0;
        var sw = Stopwatch.StartNew();
        do
        {
            long offset = random.NextInt64(workload.FileBytes / Small) * Small;
            if (write) file.Write(buffer, Small, offset, ct); else file.Read(buffer, Small, offset, ct);
            operations++;
            if ((operations & 63) == 0)
                progress(new(phase, start + .15 * Math.Min(1, sw.Elapsed / workload.RandomDuration)));
        } while (sw.Elapsed < workload.RandomDuration);
        double seconds = sw.Elapsed.TotalSeconds;
        ct.ThrowIfCancellationRequested();
        return (operations / seconds, seconds / operations * 1e6);
    }

    internal static BenchmarkMeasurement RunLoading(string parent, Action<DiskProgress> progress, CancellationToken ct,
        DiskWorkload workload, Func<string, bool, IDirectFile> open)
    {
        ct.ThrowIfCancellationRequested();
        using var workspace = new BenchmarkWorkspace(parent, workload.FileBytes + (long)workload.AssetCount * workload.AssetBytes + (512L << 20));
        using var buffer = new AlignedBuffer(Block);
        new Random(7).NextBytes(buffer.Span);
        string pack = workspace.FilePath("map.pak");
        progress(new("Preparando pacote e assets", 0));
        using (var file = open(pack, true))
            for (long offset = 0; offset < workload.FileBytes; offset += Block)
            {
                file.Write(buffer, Block, offset, ct);
                progress(new("Preparando pacote", .25 * (offset + Block) / workload.FileBytes));
            }
        var assets = Enumerable.Range(0, workload.AssetCount).Select(i => workspace.FilePath($"asset{i:D4}.bin")).ToArray();
        for (int i = 0; i < assets.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            using var file = open(assets[i], true);
            file.Write(buffer, workload.AssetBytes, 0, ct);
            progress(new("Preparando assets", .25 + .25 * (i + 1) / assets.Length));
        }
        progress(new("Lendo pacote", .5));
        var timer = Stopwatch.StartNew();
        using (var file = open(pack, false))
            for (long offset = 0; offset < workload.FileBytes; offset += Block)
            {
                file.Read(buffer, Block, offset, ct);
                progress(new("Lendo pacote", .5 + .2 * (offset + Block) / workload.FileBytes));
            }
        double packSeconds = timer.Elapsed.TotalSeconds;
        void ReadAssets()
        {
            Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, worker =>
            {
                using var local = new AlignedBuffer(workload.AssetBytes);
                for (int i = worker; i < assets.Length; i += 8)
                {
                    ct.ThrowIfCancellationRequested();
                    using var file = open(assets[i], false);
                    file.Read(local, workload.AssetBytes, 0, ct);
                }
            });
        }
        progress(new("Passada de preparação dos assets · não medida", .7));
        ReadAssets();
        progress(new("Lendo assets · até 8 workers", .8));
        timer.Restart();
        ReadAssets();
        double assetSeconds = timer.Elapsed.TotalSeconds;
        ct.ThrowIfCancellationRequested();
        double seq = workload.FileBytes / 1048576.0 / packSeconds;
        double small = (long)workload.AssetCount * workload.AssetBytes / 1048576.0 / assetSeconds;
        double fps = workload.AssetCount / assetSeconds;
        progress(new("Concluído; limpando arquivos", 1));
        return new($"Carregamento: pacote {seq:N1} MiB/s · assets {small:N1} MiB/s · {fps:N0} arquivos/s · total {packSeconds + assetSeconds:N2} s", null,
            new() { ["packReadMiBs"] = seq, ["assetsReadMiBs"] = small, ["filesPerSecond"] = fps,
                ["loadSeconds"] = packSeconds + assetSeconds, ["packSeconds"] = packSeconds, ["assetSeconds"] = assetSeconds });
    }
}
