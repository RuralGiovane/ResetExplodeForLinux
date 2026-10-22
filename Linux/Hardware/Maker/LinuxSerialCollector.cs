using System.Globalization;

namespace ResetExplode.Hardware;

public sealed record SerialPortEntry(string Name, bool ListedInSysfs, bool ListedInDev, string? DeviceNumber)
{
    public SerialUsbIdentity? Usb { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
    public string AliasDescription => Aliases.Count == 0 ? "Nenhum caminho by-id associado observado."
        : "Caminhos by-id: " + string.Join(" · ", Aliases);
    public string Title => "/dev/" + Name;
    public string HardwareDescription => Usb == null ? "Identificação USB indisponível."
        : $"Dispositivo USB pai: {Usb.Manufacturer ?? "fabricante indisponível"} · {Usb.Product ?? "produto indisponível"} · VID:PID {Usb.VendorId ?? "????"}:{Usb.ProductId ?? "????"} · driver associado: {Usb.Driver ?? "indisponível"}";
    public string Details => $"sysfs: {(ListedInSysfs ? "listado" : "não observado")} · /dev: {(ListedInDev ? "listado" : "não observado")} · major:minor: {DeviceNumber ?? "indisponível"}";
}

public sealed record SerialUsbIdentity(string DevicePath, string? VendorId, string? ProductId,
    string? Manufacturer, string? Product, string? Driver, string? DriverDevicePath);

public sealed record SerialGroupEntry(string Name, uint? Id, bool? Active)
{
    public string Description => $"{Name}: GID {Id?.ToString(CultureInfo.InvariantCulture) ?? "indisponível"} · grupo ativo nesta sessão: {Active switch { true => "sim", false => "não", _ => "indisponível" }}";
}

public sealed record SerialInventory(DateTimeOffset Timestamp, IReadOnlyList<SerialPortEntry> Ports,
    IReadOnlyList<SerialGroupEntry> Groups, IReadOnlyList<string> Notices);

/// <summary>Inventário de ttyUSB/ttyACM. Não abre portas nem testa comunicação.</summary>
public sealed class LinuxSerialCollector(LinuxFiles files)
{
    public SerialInventory Collect(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var sys = Names(files.Directories("/sys/class/tty", "tty*"));
        var dev = Names(files.Files("/dev", "tty*"));
        var aliases = ReadAliases(dev, cancellation, out int unassociated);
        var ports = new List<SerialPortEntry>();
        foreach (string name in sys.Union(dev).OrderBy(n => n, StringComparer.Ordinal))
        {
            cancellation.ThrowIfCancellationRequested();
            string? number = files.Read($"/sys/class/tty/{name}/dev");
            string[] parts = number?.Split(':') ?? [];
            if (parts.Length != 2 || !TryId(parts[0], out _) || !TryId(parts[1], out _)) number = null;
            ports.Add(new(name, sys.Contains(name), dev.Contains(name), number)
            {
                Usb = sys.Contains(name) ? ReadUsb(name, cancellation) : null,
                Aliases = aliases.TryGetValue(name, out var paths) ? Array.AsReadOnly(paths.ToArray()) : Array.Empty<string>()
            });
        }
        var groups = ReadGroups();
        var notices = new List<string>
        {
            "Listagem não comprova acesso à porta. ACLs, regras udev, isolamento e porta ocupada não são avaliados.",
            "Grupos identificados somente em /etc/group; serviços externos de identidade não são consultados."
        };
        if (ports.Count == 0) notices.Add("Nenhuma porta ttyUSB/ttyACM observada, ou diretórios indisponíveis.");
        if (unassociated > 0) notices.Add($"{unassociated} entrada(s) by-id sem associação: destino indisponível, inválido ou fora das portas observadas.");
        if (groups.Any(g => g.Active == null)) notices.Add("Associação aos grupos seriais parcialmente indisponível.");
        cancellation.ThrowIfCancellationRequested();
        return new(DateTimeOffset.Now, ports, groups, notices);
    }

    private static HashSet<string> Names(IEnumerable<string> paths) => paths.Select(Path.GetFileName)
        .Where(n => n != null && ((n.StartsWith("ttyUSB", StringComparison.Ordinal) && Digits(n[6..])) ||
                                 (n.StartsWith("ttyACM", StringComparison.Ordinal) && Digits(n[6..]))))
        .Select(n => n!).ToHashSet(StringComparer.Ordinal);

    private static bool Digits(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);

    private Dictionary<string, List<string>> ReadAliases(HashSet<string> devices, CancellationToken cancellation, out int unassociated)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        unassociated = 0;
        foreach (string alias in files.Files("/dev/serial/by-id", "*"))
        {
            cancellation.ThrowIfCancellationRequested();
            string? target = files.FileLinkTarget(alias);
            string? name = target == null ? null : Path.GetFileName(target);
            if (name == null || target != "/dev/" + name || !devices.Contains(name))
            {
                unassociated++;
                continue;
            }
            if (!result.TryGetValue(name, out var paths)) result[name] = paths = [];
            paths.Add(alias);
        }
        return result;
    }
    private static bool TryId(string text, out uint value) => uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    private SerialUsbIdentity? ReadUsb(string name, CancellationToken cancellation)
    {
        string? path = files.DirectoryTarget($"/sys/class/tty/{name}");
        if (path?.StartsWith("/sys/devices/", StringComparison.Ordinal) != true) return null;
        string? driver = null, driverPath = null;
        for (int depth = 0; depth < 32 && path.StartsWith("/sys/devices/", StringComparison.Ordinal); depth++)
        {
            cancellation.ThrowIfCancellationRequested();
            string? vendor = files.Read(path + "/idVendor");
            string? product = files.Read(path + "/idProduct");
            bool usbDevice = files.Read(path + "/uevent")?.Split('\n').Contains("DEVTYPE=usb_device") == true;
            // Parar no dispositivo mais próximo mesmo com atributos incompletos:
            // nunca preencher dados ausentes com a identidade de um hub ancestral.
            if (usbDevice || vendor != null || product != null)
                return new(path, HexId(vendor), HexId(product), Text(files.Read(path + "/manufacturer")),
                    Text(files.Read(path + "/product")), driver, driverPath);
            if (driver == null)
            {
                string? target = files.DirectoryTarget(path + "/driver");
                if (target?.StartsWith("/sys/bus/", StringComparison.Ordinal) == true && target.Contains("/drivers/", StringComparison.Ordinal))
                {
                    driver = Path.GetFileName(target);
                    driverPath = path;
                }
            }
            path = Path.GetDirectoryName(path)!;
        }
        return null;
    }

    private static string? HexId(string? value) => value?.Length == 4 && value.All(char.IsAsciiHexDigit)
        ? value.ToUpperInvariant() : null;
    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private SerialGroupEntry[] ReadGroups()
    {
        string[] status = files.Read("/proc/self/status")?.Split('\n') ?? [];
        string[]? Field(string key) => status.FirstOrDefault(l => l.StartsWith(key + ":", StringComparison.Ordinal))?
            .Split(':', 2)[1].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        string[]? gids = Field("Gid"), supplementary = Field("Groups");
        var active = new HashSet<uint>();
        bool complete = gids?.Length == 4 && supplementary != null;
        if (gids?.Length == 4)
        {
            // Grupo efetivo; não confundir GID real/salvo com associação ativa.
            if (TryId(gids[1], out uint effective)) active.Add(effective);
            else complete = false;
        }
        foreach (string item in supplementary ?? [])
            if (TryId(item, out uint id)) active.Add(id); else complete = false;
        string[] entries = files.Read("/etc/group")?.Split('\n') ?? [];
        return new[] { "dialout", "uucp" }.Select(name =>
        {
            var matches = entries.Select(l => l.Split(':')).Where(f => f.Length == 4 && f[0] == name).ToArray();
            uint? id = matches.Length == 1 && TryId(matches[0][2], out uint parsed) ? parsed : null;
            return new SerialGroupEntry(name, id, id.HasValue && complete ? active.Contains(id.Value) : null);
        }).ToArray();
    }
}
