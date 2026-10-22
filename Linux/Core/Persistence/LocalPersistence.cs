using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResetExplode;

/// <summary>JSON versionado, sem credenciais. Métodos síncronos; gravações devem ocorrer fora da UI.</summary>
public sealed class LocalPersistence(XdgPaths paths)
{
    public const int MaximumEntries = 200;
    private const int MaximumFileBytes = 2 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public StoredData<LinuxSettings> LoadSettings() => Read(paths.SettingsFile, new LinuxSettings(), ValidateSettings);
    public StoredData<BenchmarkHistory> LoadHistory() => Read(paths.HistoryFile, new BenchmarkHistory(), ValidateHistory);

    public string? SaveSettings(LinuxSettings settings)
    {
        ValidateSettings(settings);
        using var fileLock = Acquire(paths.SettingsFile);
        string? notice = PreserveBeforeWrite(paths.SettingsFile, LoadSettings());
        WriteAtomic(paths.SettingsFile, settings);
        return notice;
    }

    public StoredData<BenchmarkHistory> AppendHistory(IEnumerable<BenchmarkHistoryEntry> additions)
    {
        var incoming = additions.ToList();
        foreach (var entry in incoming) ValidateEntry(entry);
        using var fileLock = Acquire(paths.HistoryFile);
        var current = LoadHistory();
        string? notice = PreserveBeforeWrite(paths.HistoryFile, current);
        var entries = current.Value.Entries.Concat(incoming).DistinctBy(e => e.Id)
            .OrderByDescending(e => e.Date).ThenBy(e => e.Id).Take(MaximumEntries).ToList();
        var updated = new BenchmarkHistory { Entries = entries };
        WriteAtomic(paths.HistoryFile, updated);
        return new(updated, notice);
    }

    private static StoredData<T> Read<T>(string path, T defaults, Action<T> validate)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > MaximumFileBytes)
                return new(defaults, $"Arquivo excede o tamanho aceito e será preservado: {path}", false);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Documento precisa ser um objeto.");
            if (!document.RootElement.TryGetProperty("schemaVersion", out var version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) || number != 1)
                return new(defaults, $"Formato/versão não suportado; arquivo preservado: {path}", false);
            var value = document.Deserialize<T>(Json) ?? throw new JsonException("Documento vazio.");
            validate(value);
            return new(value);
        }
        catch (FileNotFoundException) { return new(defaults); }
        catch (DirectoryNotFoundException) { return new(defaults); }
        catch (JsonException ex)
        {
            return new(defaults, $"Dados inválidos em {path}: {ex.Message} O original será copiado antes de uma nova gravação.", true, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return new(defaults, $"Não foi possível ler {path}: {ex.Message}", false); }
    }

    private static void ValidateSettings(LinuxSettings settings)
    {
        if (settings.SchemaVersion != 1 || !double.IsFinite(settings.ThermalLimit) ||
            settings.ThermalLimit < CpuThermalPolicy.MinimumLimit || settings.ThermalLimit > CpuThermalPolicy.MaximumLimit)
            throw new JsonException("Configuração térmica inválida.");
    }

    private static void ValidateHistory(BenchmarkHistory history)
    {
        if (history.SchemaVersion != 1 || history.Entries == null) throw new JsonException("Histórico inválido.");
        foreach (var entry in history.Entries) ValidateEntry(entry);
    }

    private static void ValidateEntry(BenchmarkHistoryEntry entry)
    {
        if (entry == null || entry.Id == Guid.Empty || entry.Date == default || string.IsNullOrWhiteSpace(entry.Test) ||
            string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.Summary) ||
            entry.Metrics == null || entry.Context == null || !Enum.IsDefined(entry.Outcome) ||
            entry.Score is double score && !double.IsFinite(score) ||
            entry.Metrics.Any(m => !double.IsFinite(m.Value))) throw new JsonException("Registro de benchmark inválido.");
        if (entry.Outcome != BenchmarkOutcome.Completed && (entry.Score != null || entry.Metrics.Count != 0))
            throw new JsonException("Uma execução incompleta não pode conter pontuação ou métricas de resultado.");
        ValidateSettings(new LinuxSettings { ThermalLimit = entry.ThermalLimit });
    }

    private static string? PreserveBeforeWrite<T>(string path, StoredData<T> current)
    {
        if (!current.CanWrite) throw new IOException(current.Notice);
        if (!current.NeedsBackup) return null;
        string backup = path + $".invalid-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        File.Copy(path, backup, overwrite: false); // Se falhar, não substituir o original.
        return $"Original inválido preservado em {backup}";
    }

    private static FileStream Acquire(string path)
    {
        string directory = Path.GetDirectoryName(path)!;
        if (OperatingSystem.IsLinux()) Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        else Directory.CreateDirectory(directory);
        // Não apagar o arquivo de lock: outra instância pode estar aguardando no mesmo inode.
        return OpenPrivate(path + ".lock", FileMode.OpenOrCreate);
    }

    private static FileStream OpenPrivate(string path, FileMode mode)
    {
        var options = new FileStreamOptions { Mode = mode, Access = FileAccess.ReadWrite, Share = FileShare.None };
        if (OperatingSystem.IsLinux()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(path, options);
    }

    private static void WriteAtomic<T>(string path, T value)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (bytes.Length > MaximumFileBytes) throw new IOException("Os dados excedem o tamanho máximo do arquivo.");
        string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = OpenPrivate(temp, FileMode.CreateNew))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(temp); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
