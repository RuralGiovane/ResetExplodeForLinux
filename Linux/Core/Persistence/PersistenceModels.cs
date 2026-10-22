namespace ResetExplode;

public sealed record LinuxSettings
{
    public int SchemaVersion { get; init; } = 1;
    public bool ThermalEnabled { get; init; } = true;
    public double ThermalLimit { get; init; } = CpuThermalPolicy.DefaultLimit;
}

public enum BenchmarkOutcome { Completed, NotStarted, ThermalStopped, Cancelled, Failed }
public sealed record BenchmarkMeasurement(string Summary, double? Score, Dictionary<string, double> Metrics);

public sealed record BenchmarkHistoryEntry
{
    public Guid Id { get; init; }
    public DateTimeOffset Date { get; init; }
    public string Test { get; init; } = "";
    public string Name { get; init; } = "";
    public BenchmarkOutcome Outcome { get; init; }
    public string Summary { get; init; } = "";
    public double? Score { get; init; }
    public Dictionary<string, double> Metrics { get; init; } = new();
    public Dictionary<string, string> Context { get; init; } = new();
    public bool ThermalEnabled { get; init; }
    public double ThermalLimit { get; init; } = CpuThermalPolicy.DefaultLimit;
}

public sealed record BenchmarkHistory
{
    public int SchemaVersion { get; init; } = 1;
    public List<BenchmarkHistoryEntry> Entries { get; init; } = new();
}

public sealed record StoredData<T>(T Value, string? Notice = null, bool CanWrite = true, bool NeedsBackup = false);

public sealed record XdgPaths(string SettingsFile, string HistoryFile)
{
    public static XdgPaths ForCurrentUser() => Resolve(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"), Environment.GetEnvironmentVariable("XDG_STATE_HOME"));

    public static XdgPaths Resolve(string home, string? config, string? state)
    {
        if (!Path.IsPathFullyQualified(home)) throw new ArgumentException("Diretório do usuário não disponível.", nameof(home));
        string Base(string? candidate, string fallback) => !string.IsNullOrWhiteSpace(candidate) && Path.IsPathFullyQualified(candidate)
            ? candidate : Path.Combine(home, fallback);
        return new(Path.Combine(Base(config, ".config"), "ResetExplode", "settings.json"),
            Path.Combine(Base(state, ".local/state"), "ResetExplode", "benchmarks.json"));
    }
}
