namespace ResetExplode.Hardware;

public enum SensorKind { Temperature, Fan, Voltage, Current, Power, Clock, Load, Memory }
public enum HardwareComponent { Cpu, Gpu, Other }

public sealed record SensorReading(string Id, string Hardware, string Label, SensorKind Kind,
    HardwareComponent Component, double? Value, string Unit, string? Error = null)
{
    public string DisplayValue => Value is double value ? $"{value:N1} {Unit}" : "Indisponível";
}

public sealed record MemoryStatus(long? TotalBytes, long? AvailableBytes)
{
    public long? UsedBytes => TotalBytes is long total && AvailableBytes is long available ? total - available : null;
    public double? UsagePercent => TotalBytes is > 0 && UsedBytes is long used ? 100.0 * used / TotalBytes.Value : null;
}

public sealed record SystemEntry(string Name, string Value);
public sealed record LinuxSystemInfo(IReadOnlyList<SystemEntry> Entries);
public sealed record GpuSnapshot(IReadOnlyList<SensorReading> Sensors, string Status);

public interface IGpuSensorSource : IDisposable
{
    GpuSnapshot Read(CancellationToken cancellation);
}

public sealed record TelemetrySnapshot(DateTimeOffset Timestamp, double? CpuLoadPercent,
    MemoryStatus Memory, IReadOnlyList<SensorReading> Sensors, IReadOnlyList<string> Notices)
{
    public long CollectionStartedTimestamp { get; init; }
    // Não atribuir temperatura de ACPI, chipset ou placa-mãe à CPU por adivinhação.
    public double? CpuTemperature => Sensors
        .Where(s => s.Component == HardwareComponent.Cpu && s.Kind == SensorKind.Temperature)
        .Select(s => s.Value).Max();
}
