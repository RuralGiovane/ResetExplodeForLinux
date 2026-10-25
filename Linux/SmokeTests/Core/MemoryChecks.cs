using ResetExplode;

internal static class MemoryChecks
{
    public static void Run()
    {
        // Núcleos não divisores do buffer e caudas de 1–3 elementos expõem a regressão original.
        foreach (int length in new[] { 1, 3, 7, 31, 65539, 262147 })
        foreach (int threads in new[] { 1, 3, 6, 7 })
        {
            var src = Enumerable.Range(1, length).Select(i => (long)i).ToArray();
            var dst = new long[length];
            long expected = (long)length * (length + 1) / 2;
            if (MemoryBench.ReadBuffer(src, threads, CancellationToken.None) != expected)
                throw new InvalidOperationException($"Leitura incompleta: {length} elementos, {threads} threads.");
            MemoryBench.CopyBuffer(src, dst, threads, CancellationToken.None);
            if (!src.SequenceEqual(dst)) throw new InvalidOperationException("Cópia incompleta.");
        }

        var source = Enumerable.Range(1, 262147).Select(i => (long)i).ToArray();
        var destination = new long[source.Length];
        var rates = MemoryBench.MeasureBandwidth(source, destination, 3, CancellationToken.None);
        if (!double.IsFinite(rates.Read) || rates.Read <= 0 || !double.IsFinite(rates.Copy) || rates.Copy <= 0
            || !source.SequenceEqual(destination))
            throw new InvalidOperationException("Banda não produziu taxas válidas/cópia completa.");

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        foreach (Action operation in new Action[]
        {
            () => MemoryBench.ReadBuffer(source, 3, cancelled.Token),
            () => MemoryBench.CopyBuffer(source, destination, 3, cancelled.Token),
            () => MemoryBench.MeasureBandwidth(source, destination, 3, cancelled.Token)
        })
        {
            try { operation(); }
            catch (OperationCanceledException) { continue; }
            throw new InvalidOperationException("Operação de RAM ignorou cancelamento.");
        }
        Console.WriteLine("OK: RAM com partições desiguais, caudas, cópia integral, taxas e cancelamento.");
    }
}
