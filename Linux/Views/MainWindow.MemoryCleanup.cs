using Avalonia.Interactivity;

namespace ResetExplode;

public partial class MainWindow
{
    private bool _cleaningMemory;
    private Task _memoryCleanupWork = Task.CompletedTask;
    private async void ReleaseTestMemory(object? sender, RoutedEventArgs e)
    {
        if (_closeWhenStopped || _cleaningMemory || _clearingHistory || _cancellation != null || _exporting ||
            _latencyCancellation != null || _throughputCancellation != null || _dnsCancellation != null) return;
        _memoryCleanupWork = ReleaseTestMemoryAsync();
        await _memoryCleanupWork;
    }
    private async Task ReleaseTestMemoryAsync()
    {
        _cleaningMemory = true;
        SetRunning(false);
        MemoryCleanupStatusText.Text = "Liberando memória temporária do aplicativo…";
        try
        {
            _renderer = null;
            _simulation = null;
            var result = await Task.Run(BenchmarkMemoryCleanup.Release);
            if (!_closeWhenStopped)
                MemoryCleanupStatusText.Text = $"Coleta concluída. Memória gerenciada do app: {result.ManagedBytesBefore / 1048576.0:N1} → {result.ManagedBytesAfter / 1048576.0:N1} MiB. Histórico preservado. O uso total mostrado pelo sistema pode diferir.";
        }
        catch (Exception ex) { if (!_closeWhenStopped) MemoryCleanupStatusText.Text = $"Não foi possível concluir a limpeza: {ex.Message}"; }
        finally { _cleaningMemory = false; SetRunning(false); }
    }
}
