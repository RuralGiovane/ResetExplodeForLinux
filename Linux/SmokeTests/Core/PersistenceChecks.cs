using System.Globalization;
using System.Text.Json;
using ResetExplode;

internal static class PersistenceChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Fails<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Era esperada uma falha {typeof(T).Name}.");
    }

    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "reset-storage-" + Guid.NewGuid().ToString("N"));
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            var paths = XdgPaths.Resolve(root, "relativo/config", "");
            Check(paths.SettingsFile == Path.Combine(root, ".config/ResetExplode/settings.json"), "XDG relativo não foi ignorado.");
            Check(paths.HistoryFile == Path.Combine(root, ".local/state/ResetExplode/benchmarks.json"), "Fallback de estado XDG incorreto.");
            var custom = XdgPaths.Resolve(root, Path.Combine(root, "config"), Path.Combine(root, "state"));
            Check(custom.SettingsFile == Path.Combine(root, "config/ResetExplode/settings.json") &&
                custom.HistoryFile == Path.Combine(root, "state/ResetExplode/benchmarks.json"), "XDG absoluto não foi respeitado.");

            var store = new LocalPersistence(paths);
            Check(store.LoadSettings().Value.ThermalEnabled && store.LoadSettings().Value.ThermalLimit == 90,
                "Configuração ausente não usou padrões.");
            Check(!Directory.Exists(root), "Leitura de padrões criou arquivos desnecessariamente.");
            var settings = new LinuxSettings { ThermalEnabled = false, ThermalLimit = 78 };
            store.SaveSettings(settings);
            Check(new LocalPersistence(paths).LoadSettings().Value == settings, "Configuração não sobreviveu à reabertura.");
            if (OperatingSystem.IsLinux())
                Check((File.GetUnixFileMode(paths.SettingsFile) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                    UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) == 0, "Configuração não foi criada com permissão restrita.");

            string broken = "{json incompleto";
            File.WriteAllText(paths.SettingsFile, broken);
            Check(store.LoadSettings().NeedsBackup, "JSON inválido não foi identificado.");
            string? notice = store.SaveSettings(settings);
            string backup = Directory.GetFiles(Path.GetDirectoryName(paths.SettingsFile)!, "settings.json.invalid-*").Single();
            Check(notice != null && File.ReadAllText(backup) == broken && store.LoadSettings().Value == settings,
                "Recuperação perdeu o original inválido.");

            foreach (string unknown in new[] { "{\"schemaVersion\":2}", "{\"schemaVersion\":\"futuro\"}", "{}" })
            {
                File.WriteAllText(paths.SettingsFile, unknown);
                Check(!store.LoadSettings().CanWrite, "Versão desconhecida foi aceita para gravação.");
                Fails<IOException>(() => store.SaveSettings(settings));
                Check(File.ReadAllText(paths.SettingsFile) == unknown, "Versão desconhecida foi sobrescrita.");
            }
            File.WriteAllText(paths.SettingsFile, "{\"schemaVersion\":1,\"thermalLimit\":500}");
            Check(store.LoadSettings().NeedsBackup && store.LoadSettings().Value.ThermalLimit == 90,
                "Limite inválido foi aplicado à interface.");
            store.SaveSettings(settings);

            var completed = new BenchmarkHistoryEntry
            {
                Id = Guid.NewGuid(), Date = DateTimeOffset.UtcNow, Test = "cpu-render", Name = "CPU",
                Outcome = BenchmarkOutcome.Completed, Summary = "Teste completo", Score = 12.5,
                Metrics = new() { ["seconds"] = 1.25 }, Context = new() { ["cpu"] = "CPU simulada" }
            };
            store.AppendHistory([completed]);
            var reopened = new LocalPersistence(paths);
            var persisted = reopened.LoadHistory().Value.Entries.Single();
            Check(persisted.Id == completed.Id && persisted.Score == 12.5 && persisted.Metrics["seconds"] == 1.25,
                "Histórico estruturado falhou no round-trip pt-BR.");
            reopened.AppendHistory([completed]);
            Check(store.LoadHistory().Value.Entries.Count == 1, "Repetir gravação duplicou o registro.");
            string beforeInvalid = File.ReadAllText(paths.HistoryFile);
            Fails<JsonException>(() => store.AppendHistory([completed with { Id = Guid.NewGuid(), Outcome = BenchmarkOutcome.Cancelled }]));
            Check(File.ReadAllText(paths.HistoryFile) == beforeInvalid, "Resultado parcial alterou o histórico.");
            var cancelled = completed with
            {
                Id = Guid.NewGuid(), Outcome = BenchmarkOutcome.Cancelled, Summary = "Usuário cancelou", Score = null, Metrics = new()
            };
            store.AppendHistory([cancelled]);
            Check(reopened.LoadHistory().Value.Entries.Any(e => e.Id == cancelled.Id && e.Score == null && e.Metrics.Count == 0),
                "Cancelamento não foi guardado sem pontuação.");

            using (var held = new FileStream(paths.HistoryFile + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Fails<IOException>(() => reopened.AppendHistory([completed]));
            Check(store.LoadHistory().Value.Entries.Count == 2, "Falha de lock alterou o histórico.");

            var many = Enumerable.Range(0, 205).Select(i => completed with
            { Id = Guid.NewGuid(), Date = completed.Date.AddSeconds(i + 1) }).ToArray();
            var retained = store.AppendHistory(many).Value.Entries;
            Check(retained.Count == LocalPersistence.MaximumEntries && retained[0].Id == many[^1].Id &&
                !retained.Any(e => e.Id == completed.Id), "Retenção não preservou as execuções mais recentes.");
            File.WriteAllText(paths.HistoryFile, "{quebrado");
            store.AppendHistory([completed]);
            Check(Directory.GetFiles(Path.GetDirectoryName(paths.HistoryFile)!, "benchmarks.json.invalid-*").Length == 1,
                "Histórico corrompido não foi preservado.");
            Check(store.LoadHistory().Value.Entries.Single().Id == completed.Id, "Não foi possível continuar após recuperação.");
            Check(!Directory.EnumerateFiles(root, "*.tmp-*", SearchOption.AllDirectories).Any(), "Restaram arquivos temporários.");
            string blocked = Path.Combine(root, "arquivo-no-lugar-de-diretorio");
            File.WriteAllText(blocked, "não sobrescrever");
            var unavailable = new LocalPersistence(new(Path.Combine(blocked, "settings.json"), paths.HistoryFile));
            Fails<IOException>(() => unavailable.SaveSettings(settings));
            Check(File.ReadAllText(blocked) == "não sobrescrever", "Falha de diretório destruiu um arquivo existente.");
            string settingsBeforeClear = File.ReadAllText(paths.SettingsFile);
            string historyBeforeClear = File.ReadAllText(paths.HistoryFile);
            using (var held = new FileStream(paths.HistoryFile + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Fails<IOException>(() => store.ClearHistory());
            Check(File.ReadAllText(paths.HistoryFile) == historyBeforeClear, "Limpeza sem lock alterou o histórico.");
            store.ClearHistory();
            Check(new LocalPersistence(paths).LoadHistory().Value.Entries.Count == 0, "Histórico não ficou vazio após reabrir.");
            Check(File.ReadAllText(paths.SettingsFile) == settingsBeforeClear, "Limpeza alterou configuração.");
            var emptyReport = BenchmarkReportBuilder.Capture(store.LoadHistory().Value.Entries, [], "test");
            Check(emptyReport.Benchmarks.Count == 0 && BenchmarkReportBuilder.Html(emptyReport).Contains("Nenhuma execução registrada"), "Exportação ressuscitou histórico apagado.");
            store.ClearHistory();
            store.AppendHistory([cancelled]);
            Check(store.LoadHistory().Value.Entries.Single().Id == cancelled.Id, "Nova gravação restaurou registros antigos.");
            const string futureHistory = "{\"schemaVersion\":99,\"entries\":[]}";
            File.WriteAllText(paths.HistoryFile, futureHistory);
            Fails<IOException>(() => store.ClearHistory());
            Check(File.ReadAllText(paths.HistoryFile) == futureHistory, "Limpeza destruiu versão desconhecida.");
            File.WriteAllText(paths.HistoryFile, "{inválido para limpar");
            Check(store.ClearHistory() != null && store.LoadHistory().Value.Entries.Count == 0,
                "Limpeza de histórico inválido não preservou backup ou não esvaziou.");
            Check(Directory.GetFiles(Path.GetDirectoryName(paths.HistoryFile)!, "benchmarks.json.invalid-*").Any(path => File.ReadAllText(path) == "{inválido para limpar"), "Original inválido perdido na limpeza.");
            Check(!Directory.EnumerateFiles(root, "*.tmp-*", SearchOption.AllDirectories).Any(), "Limpeza deixou temporários.");
            Console.WriteLine("OK: XDG, round-trip, recuperação, versão desconhecida, lock, retenção, limpeza e exportação vazia.");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
