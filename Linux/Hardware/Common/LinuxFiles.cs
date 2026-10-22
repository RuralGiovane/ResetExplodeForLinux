using System.Globalization;

namespace ResetExplode.Hardware;

/// <summary>Acesso somente de leitura; raiz substituível permite testar /proc e /sys sem hardware.</summary>
public sealed class LinuxFiles
{
    private readonly string _root;
    public LinuxFiles(string root = "/") => _root = Path.GetFullPath(root);
    private string Resolve(string path) => Path.Combine(_root, path.TrimStart('/'));

    public string? Read(string path)
    {
        try { return File.ReadAllText(Resolve(path)).Trim(); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public string[] Directories(string path, string pattern) => List(path, pattern, true);
    public string[] Files(string path, string pattern) => List(path, pattern, false);

    internal string? FileLinkTarget(string path)
    {
        try
        {
            var entry = new FileInfo(Resolve(path));
            if (entry.LinkTarget == null) return null;
            var target = entry.ResolveLinkTarget(true);
            if (target == null || !File.Exists(target.FullName)) return null;
            string relative = Path.GetRelativePath(_root, target.FullName);
            if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative)) return null;
            return "/" + relative;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    // Retorna o caminho virtual da raiz de leitura, inclusive nas árvores de teste.
    internal string? DirectoryTarget(string path)
    {
        try
        {
            var entry = new DirectoryInfo(Resolve(path));
            var target = entry.LinkTarget == null ? entry : entry.ResolveLinkTarget(true);
            if (target == null || !Directory.Exists(target.FullName)) return null;
            string relative = Path.GetRelativePath(_root, target.FullName);
            if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative)) return null;
            return "/" + relative;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private string[] List(string path, string pattern, bool directories)
    {
        try
        {
            var entries = directories ? Directory.GetDirectories(Resolve(path), pattern)
                : Directory.GetFiles(Resolve(path), pattern);
            return entries.Select(p => path.TrimEnd('/') + "/" + Path.GetFileName(p))
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
        }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    internal static long? Integer(string? text) => long.TryParse(text, NumberStyles.Integer,
        CultureInfo.InvariantCulture, out long value) ? value : null;
}
