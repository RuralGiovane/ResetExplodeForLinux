using ResetExplode;

internal static class DiskBenchmarkChecks
{
    private static readonly DiskWorkload Small = new(4L << 20, 8, 64 << 10, TimeSpan.FromMilliseconds(20));
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void MustFail<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Esperada falha {typeof(T).Name}.");
    }

    public static void Run()
    {
        LinuxDirectFile.ValidateAlignment(0x2000, 512, 512);
        LinuxDirectFile.ValidateAlignment(0x2000, 4096, 4096);
        foreach (var alignment in new (uint Mask, uint Memory, uint Offset)[]
            { (0, 4096, 4096), (0x2000, 0, 0), (0x2000, 8192, 512), (0x2000, 512, 8192), (0x2000, 3, 512) })
            MustFail<NotSupportedException>(() => LinuxDirectFile.ValidateAlignment(alignment.Mask, alignment.Memory, alignment.Offset));
        MustFail<IOException>(() => LinuxDirectFile.ValidateTransfer(4095, 4096));
        MustFail<IOException>(() => LinuxDirectFile.ValidateTransfer(0, 4096));
        LinuxDirectFile.ValidateTransfer(4096, 4096);
        string root = Directory.CreateTempSubdirectory("reset-disk-checks-").FullName;
        string sentinel = Path.Combine(root, "keep.txt");
        File.WriteAllText(sentinel, "preservar");
        void Clean(FakeStore store)
        {
            Check(store.OpenCount == 0, "Handle simulado não liberado.");
            Check(Directory.GetFileSystemEntries(root).SequenceEqual(new[] { sentinel }), "Pasta temporária não foi limpa ou arquivo externo foi removido.");
            Check(File.ReadAllText(sentinel) == "preservar", "Arquivo do usuário alterado.");
        }
        try
        {
            var store = new FakeStore();
            var phases = new List<DiskProgress>();
            var disk = DiskBenchmarks.RunDisk(root, phases.Add, CancellationToken.None, Small, store.Open);
            Check(disk.Metrics.Values.All(v => double.IsFinite(v) && v > 0), "Métricas inválidas de disco.");
            Check(store.SequentialWritten == Small.FileBytes && store.SequentialRead == Small.FileBytes, "Contagem sequencial incorreta.");
            Check(phases.Last().Fraction == 1 && phases.Zip(phases.Skip(1)).All(p => p.First.Fraction <= p.Second.Fraction), "Progresso fora de ordem.");
            Clean(store);
            store = new FakeStore();
            var load = DiskBenchmarks.RunLoading(root, _ => { }, CancellationToken.None, Small, store.Open);
            Check(load.Metrics.Values.All(v => double.IsFinite(v) && v > 0), "Métricas inválidas de carregamento.");
            Check(store.AssetReads == 2 * Small.AssetCount, "Passada de preparação/medida não leu todos os assets.");
            Clean(store);

            foreach (bool loading in new[] { false, true })
            {
                store = new FakeStore { FailWrite = true };
                MustFail<IOException>(() => RunWork(loading, root, store, _ => { }, CancellationToken.None));
                Clean(store);
                store = new FakeStore { FailRead = true };
                MustFail<IOException>(() => RunWork(loading, root, store, _ => { }, CancellationToken.None));
                Clean(store);
                using var cancel = new CancellationTokenSource();
                store = new FakeStore();
                MustFail<OperationCanceledException>(() => RunWork(loading, root, store, p => { if (p.Fraction > 0) cancel.Cancel(); }, cancel.Token));
                Clean(store);
                store = new FakeStore();
                MustFail<OperationCanceledException>(() => RunWork(loading, root, store, _ => { }, cancel.Token));
                Check(store.TotalOpened == 0, "Pré-cancelamento criou arquivos.");
                Clean(store);
            }
            // Cancelamento durante leitura medida dos assets, após preparação e aquecimento.
            using var cancelledAssets = new CancellationTokenSource();
            store = new FakeStore { OnAssetRead = count => { if (count > Small.AssetCount) cancelledAssets.Cancel(); } };
            MustFail<OperationCanceledException>(() => RunWork(true, root, store, _ => { }, cancelledAssets.Token));
            Clean(store);
            // Falha dentro dos workers: esperar todos antes de fechar handle e limpar a pasta.
            store = new FakeStore();
            MustFail<AggregateException>(() => RunWork(false, root, store, p => { if (p.Fraction == .8) store.FailRead = true; }, CancellationToken.None));
            Clean(store);
            store = new FakeStore();
            MustFail<AggregateException>(() => RunWork(true, root, store, p => { if (p.Fraction == .8) store.FailRead = true; }, CancellationToken.None));
            Clean(store);
            MustFail<NotSupportedException>(() => DiskBenchmarks.RunDisk(root, _ => { }, CancellationToken.None, Small,
                (_, _) => throw new NotSupportedException("Sistema de arquivos não suportado")));
            Clean(store);
            MustFail<IOException>(() => { using var insufficient = new BenchmarkWorkspace(root, long.MaxValue); });
            using (var first = new BenchmarkWorkspace(root, 0))
            using (var second = new BenchmarkWorkspace(root, 0))
            {
                Check(first.DirectoryPath != second.DirectoryPath, "Colisão de diretórios.");
                File.WriteAllText(first.FilePath("own.tmp"), "primeiro");
                File.WriteAllText(second.FilePath("own.tmp"), "segundo");
            }
            Clean(store);
            var leftovers = new BenchmarkWorkspace(root, 0);
            string unexpected = Path.Combine(leftovers.DirectoryPath, "untracked.txt");
            File.WriteAllText(unexpected, "não remover recursivamente");
            MustFail<IOException>(leftovers.Dispose);
            Check(File.Exists(unexpected), "Limpeza apagou arquivo não rastreado.");
            File.Delete(unexpected);
            Directory.Delete(leftovers.DirectoryPath);
            Clean(store);
            MustFail<ArgumentException>(() => DiskBenchmarks.RunDisk("relative", _ => { }, CancellationToken.None, Small, store.Open));
            Console.WriteLine("OK: disco/carregamento simulados, alinhamento, I/O curto, métricas, cancelamento, falhas e limpeza exclusiva.");
        }
        finally { Directory.Delete(root, true); }
    }
    private static BenchmarkMeasurement RunWork(bool loading, string root, FakeStore store, Action<DiskProgress> progress, CancellationToken ct) =>
        loading ? DiskBenchmarks.RunLoading(root, progress, ct, Small, store.Open) : DiskBenchmarks.RunDisk(root, progress, ct, Small, store.Open);

    public static void RunLive(string parent)
    {
        // Opt-in: somente arquivos exclusivos, carga reduzida. Não mede desempenho representativo.
        using (var workspace = new BenchmarkWorkspace(parent, 16L << 20))
        using (var source = new AlignedBuffer(4096))
        using (var target = new AlignedBuffer(4096))
        {
            new Random(123).NextBytes(source.Span);
            string path = workspace.FilePath("roundtrip.bin");
            using var file = new LinuxDirectFile(path, true);
            file.Write(source, 4096, 0, CancellationToken.None);
            file.Read(target, 4096, 0, CancellationToken.None);
            Check(source.Span.SequenceEqual(target.Span), "Dados reais diferentes após I/O direto.");
            MustFail<IOException>(() => file.Read(target, 4096, 4096, CancellationToken.None));
            MustFail<IOException>(() => { using var collision = new LinuxDirectFile(path, true); });
        }
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            IDirectFile Open(string path, bool create) => new LinuxDirectFile(path, create);
            var disk = DiskBenchmarks.RunDisk(parent, _ => { }, cancellation.Token, Small, Open);
            var load = DiskBenchmarks.RunLoading(parent, _ => { }, cancellation.Token, Small, Open);
            Check(disk.Metrics.Values.All(v => double.IsFinite(v) && v > 0) && load.Metrics.Values.All(v => double.IsFinite(v) && v > 0), "Métricas reais inválidas.");
            Console.WriteLine("OK: I/O direto real, round-trip, EOF rejeitado, criação exclusiva e cargas reduzidas disco/carregamento. Não valida os tamanhos completos nem a UI.");
        }
        finally { Console.CancelKeyPress -= cancel; }
    }

    private sealed class FakeStore
    {
        private readonly Dictionary<string, byte[]> _files = new();
        public int OpenCount, TotalOpened, AssetReads;
        public long SequentialWritten, SequentialRead;
        public bool FailWrite, FailRead;
        public Action<int>? OnAssetRead;
        public IDirectFile Open(string path, bool create)
        {
            lock (_files)
            {
                if (create)
                {
                    _files.Add(path, new byte[path.EndsWith(".pak") || path.EndsWith("disk.bin") ? (int)Small.FileBytes : Small.AssetBytes]);
                    using var marker = new FileStream(path, FileMode.CreateNew);
                }
                Check(_files.ContainsKey(path), "Arquivo não preparado.");
            }
            Interlocked.Increment(ref OpenCount);
            Interlocked.Increment(ref TotalOpened);
            return new FakeFile(this, path);
        }
        private sealed class FakeFile(FakeStore store, string path) : IDirectFile
        {
            private bool _disposed;
            public void Read(AlignedBuffer buffer, int count, long offset, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                if (store.FailRead) throw new IOException("Leitura simulada falhou.");
                Check(!_disposed && offset % 4096 == 0 && count % 4096 == 0, "I/O inválido.");
                store._files[path].AsSpan((int)offset, count).CopyTo(buffer.Span);
                if (count == 1 << 20) Interlocked.Add(ref store.SequentialRead, count);
                if (Path.GetFileName(path).StartsWith("asset"))
                { int reads = Interlocked.Increment(ref store.AssetReads); store.OnAssetRead?.Invoke(reads); }
                ct.ThrowIfCancellationRequested();
            }
            public void Write(AlignedBuffer buffer, int count, long offset, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                if (store.FailWrite) throw new IOException("Escrita simulada falhou.");
                Check(!_disposed && offset % 4096 == 0 && count % 4096 == 0, "I/O inválido.");
                buffer.Span[..count].CopyTo(store._files[path].AsSpan((int)offset, count));
                if (count == 1 << 20) Interlocked.Add(ref store.SequentialWritten, count);
            }
            public void Dispose() { if (!_disposed) Interlocked.Decrement(ref store.OpenCount); _disposed = true; }
        }
    }
}
