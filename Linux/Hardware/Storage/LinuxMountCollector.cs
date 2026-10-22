using System.Text;

namespace ResetExplode.Hardware;

public sealed record FileSystemSpace(long TotalBytes, long FreeBytes, long AvailableBytes)
{
    public bool IsValid => TotalBytes >= 0 && FreeBytes >= 0 && FreeBytes <= TotalBytes && AvailableBytes >= 0 && AvailableBytes <= FreeBytes;
    public long UsedBytes => TotalBytes - FreeBytes;
}

public sealed record MountedFileSystem(int Id, string DeviceNumber, string Root, string MountPoint,
    string FileSystem, string Source, bool ReadOnly, FileSystemSpace? Space = null, string? Notice = null)
{
    public string Details => $"{FileSystem} · {Source} · dispositivo {DeviceNumber} · raiz {Root} · {(ReadOnly ? "somente leitura" : "leitura/escrita")}";
    public string Capacity => Space is { } s
        ? $"{s.UsedBytes / 1073741824.0:N2} GiB usados · {s.AvailableBytes / 1073741824.0:N2} GiB disponíveis ao usuário · {s.FreeBytes / 1073741824.0:N2} GiB livres totais · capacidade {s.TotalBytes / 1073741824.0:N2} GiB"
        : "Espaço indisponível";
}

public sealed record MountInventory(IReadOnlyList<MountedFileSystem> Mounts, IReadOnlyList<string> Notices);

/// <summary>Lê montagens do namespace do processo. Consultas de espaço são injetáveis nos testes.</summary>
public sealed class LinuxMountCollector(LinuxFiles files, Func<string, FileSystemSpace>? readSpace = null)
{
    // Não consultar automaticamente rede, FUSE, autofs ou pseudo-sistemas desconhecidos.
    private static readonly HashSet<string> LocalFormats = new(StringComparer.Ordinal)
    { "ext2", "ext3", "ext4", "xfs", "btrfs", "f2fs", "vfat", "exfat", "ntfs3", "tmpfs", "overlay", "squashfs", "iso9660", "udf" };

    public MountInventory Collect(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        string? text = files.Read("/proc/self/mountinfo");
        if (text == null) return new([], ["Tabela de montagens indisponível."]);
        var mounts = new List<MountedFileSystem>();
        int invalid = 0;
        foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellation.ThrowIfCancellationRequested();
            var mount = ParseLine(line);
            if (mount == null) { invalid++; continue; }
            mounts.Add(mount);
        }
        // Pontos sobrepostos não permitem atribuir uma consulta por caminho ao mount ID correto.
        var duplicates = mounts.GroupBy(m => m.MountPoint).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        for (int i = 0; i < mounts.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var mount = mounts[i];
            if (duplicates.Contains(mount.MountPoint))
            { mounts[i] = mount with { Notice = "Ponto de montagem sobreposto; espaço não consultado." }; continue; }
            if (!LocalFormats.Contains(mount.FileSystem))
            { mounts[i] = mount with { Notice = "Espaço não consultado automaticamente para este tipo de sistema de arquivos." }; continue; }
            try
            {
                var space = (readSpace ?? ReadSpace)(mount.MountPoint);
                mounts[i] = space.IsValid ? mount with { Space = space }
                    : mount with { Notice = "Contadores de espaço inconsistentes; atualize o inventário." };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { mounts[i] = mount with { Notice = $"Não foi possível consultar o espaço: {ex.Message}" }; }
        }
        cancellation.ThrowIfCancellationRequested();
        return new(mounts, invalid > 0 ? [$"{invalid} linha(s) de mountinfo inválida(s) ignorada(s)."] : []);
    }

    private static FileSystemSpace ReadSpace(string path)
    {
        var drive = new DriveInfo(path);
        return new(drive.TotalSize, drive.TotalFreeSpace, drive.AvailableFreeSpace);
    }

    public static MountedFileSystem? ParseLine(string line)
    {
        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int separator = Array.IndexOf(parts, "-");
        if (separator < 6 || parts.Length != separator + 4 ||
            !int.TryParse(parts[0], out int id) || id <= 0 ||
            !int.TryParse(parts[1], out int parent) || parent <= 0) return null;
        var device = parts[2].Split(':');
        if (device.Length != 2 || !uint.TryParse(device[0], out _) || !uint.TryParse(device[1], out _)) return null;
        string root = Unescape(parts[3]), point = Unescape(parts[4]);
        if (!root.StartsWith('/') || !point.StartsWith('/')) return null;
        bool readOnly = parts[5].Split(',').Contains("ro") || parts[separator + 3].Split(',').Contains("ro");
        return new(id, parts[2], root, point, parts[separator + 1], Unescape(parts[separator + 2]), readOnly);
    }

    private static string Unescape(string value)
    {
        var result = new StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 3 < value.Length)
            {
                char? decoded = value.Substring(i + 1, 3) switch
                { "040" => ' ', "011" => '\t', "012" => '\n', "134" => '\\', _ => null };
                if (decoded is char c) { result.Append(c); i += 3; continue; }
            }
            result.Append(value[i]);
        }
        return result.ToString();
    }
}
