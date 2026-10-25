using System.Runtime;

namespace ResetExplode;

public sealed record MemoryCleanupResult(long ManagedBytesBefore, long ManagedBytesAfter);

/// <summary>Acionar somente fora das medições. Não altera caches globais nem dados persistidos.</summary>
public static class BenchmarkMemoryCleanup
{
    public static MemoryCleanupResult Release()
    {
        long before = GC.GetTotalMemory(false);
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        return new(before, GC.GetTotalMemory(false));
    }
}
