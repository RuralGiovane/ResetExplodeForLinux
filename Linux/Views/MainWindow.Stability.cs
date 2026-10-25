using Avalonia.Interactivity;
using ResetExplode.Hardware;

namespace ResetExplode;

public partial class MainWindow
{
    private Task _stabilityWork = Task.CompletedTask;
    private readonly CancellationTokenSource _stabilityCancellation = new();
    private async void RefreshStability(object? sender, RoutedEventArgs e)
    {
        if (_closeWhenStopped || !_stabilityWork.IsCompleted) return;
        _stabilityWork = RefreshStabilityAsync();
        await _stabilityWork;
    }
    private async Task RefreshStabilityAsync()
    {
        RefreshStabilityButton.IsEnabled = false;
        StabilityStatusText.Text = "Lendo contadores EDAC…";
        StabilityDetailsText.Text = "";
        try
        {
            var snapshot = await Task.Run(() => new LinuxEdacCollector(new LinuxFiles()).Collect(_stabilityCancellation.Token));
            if (_closeWhenStopped) return;
            StabilityStatusText.Text = $"{snapshot.Status} · {snapshot.Timestamp:HH:mm:ss}";
            StabilityDetailsText.Text = string.Join(Environment.NewLine + Environment.NewLine, snapshot.Controllers.Select(c => c.Description));
        }
        catch (OperationCanceledException) when (_stabilityCancellation.IsCancellationRequested) { }
        catch (Exception ex) { if (!_closeWhenStopped) StabilityStatusText.Text = $"EDAC indisponível: {ex.Message}"; }
        finally { RefreshStabilityButton.IsEnabled = !_closeWhenStopped; }
    }
}
