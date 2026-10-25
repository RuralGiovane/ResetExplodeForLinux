using System.Runtime.InteropServices;

namespace ResetExplode;

/// <summary>Somente arquivos próprios; nunca remove recursivamente a pasta selecionada.</summary>
internal sealed class BenchmarkWorkspace : IDisposable
{
    public string DirectoryPath { get; }
    private readonly HashSet<string> _created = new(StringComparer.Ordinal);
    public BenchmarkWorkspace(string parent, long requiredBytes)
    {
        LinuxDirectFile.RequirePlatform();
        if (string.IsNullOrWhiteSpace(parent) || parent.Contains('\0') || !Path.IsPathFullyQualified(parent) || !Directory.Exists(parent))
            throw new ArgumentException("Escolha uma pasta existente com caminho absoluto.");
        if (Statvfs(parent, out var space) != 0) throw LinuxDirectFile.Error("Espaço livre indisponível", parent);
        if ((decimal)space.Available * space.FragmentSize < requiredBytes)
            throw new IOException($"Espaço insuficiente: necessários {requiredBytes / 1048576} MiB livres, incluindo margem.");
        DirectoryPath = Path.Combine(parent, ".reset-explode-bench-" + Guid.NewGuid().ToString("N"));
        if (Mkdir(DirectoryPath, 0x1C0) != 0) throw LinuxDirectFile.Error("Falha ao criar pasta exclusiva", DirectoryPath); // 0700
    }
    public string FilePath(string name)
    {
        if (Path.GetFileName(name) != name || name is "." or ".." || string.IsNullOrEmpty(name)) throw new ArgumentException("Nome inválido.");
        string path = Path.Combine(DirectoryPath, name);
        _created.Add(path);
        return path;
    }
    public void Dispose()
    {
        var failures = new List<Exception>();
        foreach (string file in _created)
            try { File.Delete(file); } catch (Exception ex) { failures.Add(ex); }
        try { Directory.Delete(DirectoryPath); } catch (Exception ex) { failures.Add(ex); }
        if (failures.Count != 0)
            throw new IOException($"Não foi possível limpar todos os arquivos temporários em {DirectoryPath}. Verifique essa pasta; resultado não publicado.", new AggregateException(failures));
    }
    [StructLayout(LayoutKind.Explicit, Size = 112)]
    private struct SpaceData
    {
        [FieldOffset(8)] public ulong FragmentSize;
        [FieldOffset(32)] public ulong Available;
    }
    [DllImport("libc", EntryPoint = "statvfs", SetLastError = true)]
    private static extern int Statvfs([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out SpaceData data);
    [DllImport("libc", EntryPoint = "mkdir", SetLastError = true)]
    private static extern int Mkdir([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);
}
