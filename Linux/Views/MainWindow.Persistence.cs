using System.Runtime.InteropServices;
using Avalonia.Interactivity;

namespace ResetExplode;

public partial class MainWindow
{
    private LocalPersistence? _store;
    private LinuxSettings? _lastSavedSettings;
    private List<BenchmarkHistoryEntry> _history = new();
    private readonly List<BenchmarkHistoryEntry> _pendingHistory = new();
    private Task _persistenceWork = Task.CompletedTask;
    private string? _settingsNotice, _historyNotice;
    private string _cpuDescription = "Indisponível";

    private void InitializePersistence()
    {
        try
        {
            _store = new LocalPersistence(XdgPaths.ForCurrentUser());
            var settings = _store.LoadSettings();
            ThermalEnabled.IsChecked = settings.Value.ThermalEnabled;
            ThermalLimit.Value = (decimal)settings.Value.ThermalLimit;
            _settingsNotice = settings.Notice;
            var history = _store.LoadHistory();
            _history = history.Value.Entries;
            _historyNotice = history.Notice;
        }
        catch (Exception ex) { _settingsNotice = $"Armazenamento indisponível: {ex.Message}"; }
        RenderHistory();
        UpdatePersistenceStatus();
    }

    private LinuxSettings CurrentSettings() => new()
    {
        ThermalEnabled = ThermalEnabled.IsChecked == true,
        ThermalLimit = (double)(ThermalLimit.Value ?? (decimal)CpuThermalPolicy.DefaultLimit)
    };

    private async void SavePreferences(object? sender, RoutedEventArgs e)
    {
        if (_closeWhenStopped || _cancellation != null || _exporting) return;
        await QueuePersistenceAsync(CurrentSettings(), forceSettings: true);
    }

    private async void RetryPersistence(object? sender, RoutedEventArgs e)
    {
        if (_closeWhenStopped || _cancellation != null || _exporting) return;
        await QueuePersistenceAsync(CurrentSettings(), forceSettings: true, reloadHistory: true);
    }

    private Task QueuePersistenceAsync(LinuxSettings settings, bool forceSettings = false, bool reloadHistory = false)
    {
        Task previous = _persistenceWork;
        return _persistenceWork = SaveAfterAsync(previous, settings, forceSettings, reloadHistory);
    }

    private async Task SaveAfterAsync(Task previous, LinuxSettings settings, bool forceSettings, bool reloadHistory)
    {
        await previous;
        if (_store == null) { UpdatePersistenceStatus(); return; }
        if (forceSettings || settings != _lastSavedSettings)
        {
            try
            {
                _settingsNotice = await Task.Run(() => _store.SaveSettings(settings));
                _lastSavedSettings = settings;
            }
            catch (Exception ex) { _settingsNotice = $"Configuração não salva: {ex.Message}"; }
        }
        if (_pendingHistory.Count > 0)
        {
            var batch = _pendingHistory.ToArray();
            try
            {
                var saved = await Task.Run(() => _store.AppendHistory(batch));
                _history = saved.Value.Entries;
                var ids = batch.Select(e => e.Id).ToHashSet();
                _pendingHistory.RemoveAll(e => ids.Contains(e.Id));
                _historyNotice = saved.Notice;
            }
            catch (Exception ex) { _historyNotice = $"Histórico não salvo; mantido nesta sessão: {ex.Message}"; }
        }
        else if (reloadHistory)
        {
            var loaded = await Task.Run(_store.LoadHistory);
            if (loaded.Notice == null) _history = loaded.Value.Entries;
            _historyNotice = loaded.Notice;
        }
        RenderHistory();
        UpdatePersistenceStatus();
    }

    private async Task RecordBenchmarkAsync(BenchmarkHistoryEntry entry, LinuxSettings settings)
    {
        _pendingHistory.Add(entry);
        RenderHistory();
        await QueuePersistenceAsync(settings);
    }

    private Dictionary<string, string> BenchmarkContext(Dictionary<string, string>? parameters)
    {
        var context = parameters == null ? new Dictionary<string, string>() : new Dictionary<string, string>(parameters);
        context["cpu"] = _cpuDescription;
        context["os"] = RuntimeInformation.OSDescription;
        context["architecture"] = RuntimeInformation.ProcessArchitecture.ToString();
        context["runtime"] = RuntimeInformation.FrameworkDescription;
        context["algorithmBase"] = "Windows v0.16";
        return context;
    }

    private void RenderHistory()
    {
        var pendingIds = _pendingHistory.Select(e => e.Id).ToHashSet();
        var rows = _pendingHistory.Concat(_history).DistinctBy(e => e.Id)
            .OrderByDescending(e => e.Date).Take(LocalPersistence.MaximumEntries);
        HistoryText.Text = string.Join(Environment.NewLine + Environment.NewLine, rows.Select(e =>
        {
            string state = e.Outcome switch
            {
                BenchmarkOutcome.Completed => "CONCLUÍDO",
                BenchmarkOutcome.NotStarted => "NÃO INICIADO",
                BenchmarkOutcome.ThermalStopped => "INTERROMPIDO",
                BenchmarkOutcome.Cancelled => "CANCELADO",
                _ => "ERRO"
            };
            string pending = pendingIds.Contains(e.Id) ? " · NÃO SALVO" : "";
            return $"{e.Date.ToLocalTime():dd/MM/yyyy HH:mm:ss} · {e.Name} · {state}{pending}\n{e.Summary}";
        }));
    }

    private void UpdatePersistenceStatus()
    {
        string notices = string.Join(Environment.NewLine, new[] { _settingsNotice, _historyNotice }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        PersistenceStatusText.Text = notices.Length > 0 ? notices
            : $"Armazenamento local pronto · {_history.Count} registro(s) · {_pendingHistory.Count} pendente(s).";
    }
}
