using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace ResetExplode;

public sealed record StressProgress(double Seconds, long AllocatedBytes, long MemoryPasses);
public sealed record StressResult(double Seconds, long AllocatedBytes, long MemoryPasses, int CpuThreads);

/// <summary>Carga CPU e padrões RAM derivados de Windows/StressTester.cs; sem APIs do SO.</summary>
public static class StressTest
{
    public const long MiB = 1024 * 1024;

    public static void ValidateMemory(long requested, long? available, long runtimeBudget)
    {
        if (requested < MiB || requested > 1024 * MiB || requested % 8 != 0)
            throw new ArgumentOutOfRangeException(nameof(requested));
        // Margem conservadora; disponibilidade pode mudar depois da leitura.
        if (available is not > 0 || runtimeBudget <= 0 || requested > Math.Min(available.Value, runtimeBudget) / 4)
            throw new InvalidOperationException("RAM disponível insuficiente ou desconhecida. Reduza a memória do teste; é necessário manter quatro vezes o tamanho solicitado disponível.");
    }

    public static StressResult Run(bool cpu, long memoryBytes, TimeSpan duration,
        Action<StressProgress>? progress, CancellationToken cancellation) =>
        Run(cpu, memoryBytes, duration, progress, cancellation, Environment.ProcessorCount);

    internal static StressResult Run(bool cpu, long memoryBytes, TimeSpan duration,
        Action<StressProgress>? progress, CancellationToken cancellation, int processors)
    {
        if ((!cpu && memoryBytes == 0) || memoryBytes < 0 || memoryBytes > 1024 * MiB || memoryBytes % 8 != 0 ||
            duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(30) || processors < 1)
            throw new ArgumentOutOfRangeException(nameof(duration), "Selecione CPU e/ou RAM e uma duração de até 30 minutos.");
        cancellation.ThrowIfCancellationRequested();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var clock = Stopwatch.StartNew();
        var workers = new List<Thread>();
        ExceptionDispatchInfo? failure = null;
        long allocated = 0, passes = 0;
        int cpuThreads = cpu ? Math.Max(1, processors - (memoryBytes > 0 ? 1 : 0)) : 0;
        bool Continue() => !stop.IsCancellationRequested && clock.Elapsed < duration;
        void Start(Action work)
        {
            var thread = new Thread(() =>
            {
                try { work(); }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref failure, ExceptionDispatchInfo.Capture(ex), null);
                    stop.Cancel();
                }
            }) { IsBackground = true, Name = "RESET Stress", Priority = ThreadPriority.BelowNormal };
            thread.Start();
            workers.Add(thread);
        }
        try
        {
            for (int worker = 0; worker < cpuThreads; worker++)
            {
                int seed = worker;
                Start(() => CpuWorker(seed, Continue));
            }
            if (memoryBytes > 0) Start(() =>
            {
                var chunks = new List<long[]>();
                while (allocated < memoryBytes && Continue())
                {
                    int count = (int)(Math.Min(8 * MiB, memoryBytes - allocated) / 8);
                    chunks.Add(new long[count]);
                    Interlocked.Add(ref allocated, count * 8L);
                }
                ulong pass = 0;
                while (Continue())
                {
                    long pattern = unchecked((long)(++pass * 0x9E3779B97F4A7C15UL));
                    foreach (var chunk in chunks)
                        if (!WritePattern(chunk, pattern, Continue)) return;
                    foreach (var chunk in chunks)
                        if (!VerifyPattern(chunk, pattern, Continue)) return;
                    Interlocked.Increment(ref passes);
                }
                GC.KeepAlive(chunks);
            });
            while (workers.Any(t => t.IsAlive))
            {
                progress?.Invoke(new(clock.Elapsed.TotalSeconds, Interlocked.Read(ref allocated), Interlocked.Read(ref passes)));
                // Não depender do pool para prazo, cancelamento ou encerramento dos workers.
                foreach (var worker in workers) worker.Join(10);
            }
            cancellation.ThrowIfCancellationRequested();
            failure?.Throw();
            return new(clock.Elapsed.TotalSeconds, allocated, passes, cpuThreads);
        }
        finally
        {
            stop.Cancel();
            foreach (var worker in workers) worker.Join();
        }
    }

    private static void CpuWorker(int seed, Func<bool> keepRunning)
    {
        var buffer = new double[4096];
        for (int i = 0; i < buffer.Length; i++) buffer[i] = i + seed + 1;
        ulong x = (ulong)(seed + 1) * 0x9E3779B97F4A7C15UL;
        while (keepRunning())
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                double v = Math.FusedMultiplyAdd(buffer[i], 1.0000001, 0.5);
                v = Math.Sqrt(v * v + 1) + Math.Sin(v) * 1e-3;
                buffer[i] = v > 1e12 ? i + 1 : v;
                x ^= x << 13; x ^= x >> 7; x ^= x << 17;
            }
            if (x == 0) buffer[0] = 0;
        }
        GC.KeepAlive(buffer);
    }

    internal static bool WritePattern(long[] buffer, long pattern, Func<bool> keepRunning)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            if ((i & 4095) == 0 && !keepRunning()) return false;
            buffer[i] = pattern ^ i;
        }
        return true;
    }

    internal static bool VerifyPattern(long[] buffer, long pattern, Func<bool> keepRunning)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            if ((i & 4095) == 0 && !keepRunning()) return false;
            if (buffer[i] != (pattern ^ i))
                throw new InvalidOperationException("Divergência na verificação de RAM. Teste interrompido; investigue a estabilidade do sistema.");
        }
        return true;
    }
}
