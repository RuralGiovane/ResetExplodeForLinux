using System.Runtime.CompilerServices;
using ResetExplode;

internal static class MemoryCleanupChecks
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateTemporary(bool fail)
    {
        var buffer = new byte[8 << 20];
        Array.Fill(buffer, (byte)42);
        var weak = new WeakReference(buffer);
        try { if (fail) throw new IOException("Falha simulada após alocação."); }
        catch (IOException) { }
        return weak;
    }
    public static void Run()
    {
        var preserved = new byte[1 << 20];
        preserved[0] = 123;
        var success = CreateTemporary(false);
        var failure = CreateTemporary(true);
        var result = BenchmarkMemoryCleanup.Release();
        if (success.IsAlive || failure.IsAlive) throw new InvalidOperationException("Buffers temporários continuam vivos após a coleta.");
        if (preserved[0] != 123) throw new InvalidOperationException("Dados vivos alterados pela limpeza.");
        GC.KeepAlive(preserved);
        if (result.ManagedBytesBefore < 0 || result.ManagedBytesAfter < 0) throw new InvalidOperationException("Contagem de memória inválida.");
        Console.WriteLine($"OK: limpeza recuperou buffers temporários e preservou dados vivos; gerenciada {result.ManagedBytesBefore / 1048576.0:N1} → {result.ManagedBytesAfter / 1048576.0:N1} MiB.");
    }
}
