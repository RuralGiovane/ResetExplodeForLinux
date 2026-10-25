using Avalonia.Interactivity;

namespace ResetExplode;

public partial class MainWindow
{
    private bool _clearingHistory;
    private Task _historyClearWork = Task.CompletedTask;
    private bool CanClearHistory => !_closeWhenStopped && !_clearingHistory && !_cleaningMemory && !_exporting &&
        _cancellation == null && _latencyCancellation == null && _throughputCancellation == null && _dnsCancellation == null;

    private void ClearSessionHistory(object? sender, RoutedEventArgs e)
    {
        if (CanClearHistory) ResultsText.Text = "";
    }
    private void RequestHistoryClear(object? sender, RoutedEventArgs e)
    {
        if (!CanClearHistory) return;
        HistoryClearStatusText.Text = "";
        ClearHistoryConfirmation.IsVisible = true;
    }
    private void DismissHistoryClear(object? sender, RoutedEventArgs e) => ClearHistoryConfirmation.IsVisible = false;
    private async void ConfirmHistoryClear(object? sender, RoutedEventArgs e)
    {
        if (!CanClearHistory || !ClearHistoryConfirmation.IsVisible) return;
        ClearHistoryConfirmation.IsVisible = false;
        _historyClearWork = ClearStoredHistoryAsync();
        await _historyClearWork;
    }
    private async Task ClearStoredHistoryAsync()
    {
        _clearingHistory = true;
        SetRunning(false);
        HistoryClearStatusText.Text = "Limpando histórico salvo…";
        try
        {
            await _persistenceWork;
            if (_store == null) throw new IOException("Armazenamento indisponível; registros preservados.");
            string? notice = await Task.Run(_store.ClearHistory);
            // Só descartar as cópias em memória quando a gravação vazia foi concluída.
            _history.Clear();
            _pendingHistory.Clear();
            _historyNotice = notice;
            RenderHistory();
            UpdatePersistenceStatus();
            HistoryClearStatusText.Text = "Histórico salvo e pendências limpos. As próximas exportações incluirão somente novas execuções.";
        }
        catch (Exception ex) { HistoryClearStatusText.Text = $"Não foi possível limpar o histórico: {ex.Message}"; }
        finally { _clearingHistory = false; SetRunning(false); }
    }
}
