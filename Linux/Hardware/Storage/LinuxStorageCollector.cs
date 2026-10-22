namespace ResetExplode.Hardware;

public sealed record BlockDevice(string Name, string? Parent, string? Model, long? CapacityBytes,
    bool? Rotational, bool? Removable, bool? ReadOnly)
{
    public string Title => $"/dev/{Name}";
    public string Capacity => CapacityBytes is long bytes ? $"{bytes / 1073741824.0:N2} GiB ({bytes:N0} bytes)" : "Capacidade indisponível";
    public string Details => Parent != null ? $"Partição de /dev/{Parent} · somente leitura: {Flag(ReadOnly)}"
        : $"Modelo: {Model ?? "indisponível"} · rotação: {Flag(Rotational)} · removível: {Flag(Removable)} · somente leitura: {Flag(ReadOnly)}";
    private static string Flag(bool? value) => value switch { true => "sim", false => "não", _ => "indisponível" };
}

public sealed record StorageInventory(DateTimeOffset Timestamp, IReadOnlyList<BlockDevice> Devices, IReadOnlyList<string> Notices);

/// <summary>Inventário somente de leitura. Não abre /dev, não consulta SMART e não infere saúde.</summary>
public sealed class LinuxStorageCollector(LinuxFiles files)
{
    public StorageInventory Collect(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var devices = new List<BlockDevice>();
        var notices = new List<string>();
        foreach (string path in files.Directories("/sys/block", "*"))
        {
            cancellation.ThrowIfCancellationRequested();
            string name = Path.GetFileName(path);
            var device = Read(path, name, null);
            devices.Add(device);
            if (device.CapacityBytes == null) notices.Add($"/dev/{name}: capacidade ausente, inválida ou dispositivo removido durante a leitura.");
            foreach (string child in files.Directories(path, "*"))
            {
                cancellation.ThrowIfCancellationRequested();
                if (LinuxFiles.Integer(files.Read(child + "/partition")) is not > 0) continue;
                var partition = Read(child, Path.GetFileName(child), name);
                devices.Add(partition);
                if (partition.CapacityBytes == null) notices.Add($"{partition.Title}: capacidade indisponível.");
            }
        }
        if (devices.Count == 0) notices.Add("Nenhum dispositivo encontrado ou /sys/block indisponível.");
        return new(DateTimeOffset.Now, devices, notices);
    }

    private BlockDevice Read(string path, string name, string? parent)
    {
        long? sectors = LinuxFiles.Integer(files.Read(path + "/size"));
        // A unidade de size é sempre 512 bytes, inclusive com blocos lógicos de 4096 bytes.
        long? bytes = sectors is >= 0 && sectors <= long.MaxValue / 512 ? sectors.Value * 512 : null;
        string? model = parent == null ? files.Read(path + "/device/model") : null;
        if (string.IsNullOrWhiteSpace(model)) model = null;
        return new(name, parent, model, bytes, Flag(path + "/queue/rotational"),
            Flag(path + "/removable"), Flag(path + "/ro"));
    }

    private bool? Flag(string path) => files.Read(path) switch { "0" => false, "1" => true, _ => null };
}
