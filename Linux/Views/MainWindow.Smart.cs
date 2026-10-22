using Avalonia.Interactivity;
using ResetExplode.Hardware;

namespace ResetExplode;

public partial class MainWindow
{
    private SmartReport? _lastSmartReport;
    private DateTimeOffset? _smartCollectedAt;
    private Task _smartWork = Task.CompletedTask;

    private async void ReadSmart(object? sender, RoutedEventArgs e)
    {
        if (_closeWhenStopped || !_smartWork.IsCompleted || SmartDevice.SelectedItem is not BlockDevice device) return;
        _smartWork = ReadSmartAsync(device);
        await _smartWork;
    }

    private async Task ReadSmartAsync(BlockDevice device)
    {
        _lastSmartReport = null;
        _smartCollectedAt = null;
        SmartReadButton.IsEnabled = SmartDevice.IsEnabled = false;
        SmartResultText.Text = $"Consultando {device.Title}…";
        try
        {
            var result = await new LinuxSmartReader().ReadAsync(device.Name, _storageCancellation.Token);
            if (_closeWhenStopped) return;
            _lastSmartReport = result;
            _smartCollectedAt = DateTimeOffset.UtcNow;
            SmartResultText.Text = $"{result.Device} · {result.Status} · {DateTime.Now:HH:mm:ss}\n" +
                string.Join(Environment.NewLine, result.Metrics.Select(m => $"{m.Name}: {m.Value}").Concat(result.Notices));
        }
        catch (OperationCanceledException) when (_storageCancellation.IsCancellationRequested) { }
        catch (Exception ex) { SmartResultText.Text = $"Consulta indisponível: {ex.Message}"; }
        finally { SmartReadButton.IsEnabled = SmartDevice.IsEnabled = !_closeWhenStopped; }
    }
}
