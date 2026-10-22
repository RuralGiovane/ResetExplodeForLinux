using Avalonia.Interactivity;
using ResetExplode.Hardware;

namespace ResetExplode;

public partial class MainWindow
{
    private PowerInventory? _powerInventory;
    private string? _powerCollectionError;
    private Task _powerWork = Task.CompletedTask;
    private Task _powerLoop = Task.CompletedTask;
    private bool _powerMonitoringStarted;
    private readonly CancellationTokenSource _powerCancellation = new();

    private async void RefreshPower(object? sender, RoutedEventArgs e) => await StartPowerRefresh();

    private void StartPowerMonitoring()
    {
        if (_powerMonitoringStarted || _closeWhenStopped) return;
        _powerMonitoringStarted = true;
        _powerLoop = MonitorPowerAsync();
    }

    private async Task MonitorPowerAsync()
    {
        try
        {
            while (!_powerCancellation.IsCancellationRequested)
            {
                // Chamadas automáticas/manuais compartilham a coleta em andamento na thread da UI.
                await StartPowerRefresh();
                await Task.Delay(TimeSpan.FromSeconds(5), _powerCancellation.Token);
            }
        }
        catch (OperationCanceledException) when (_powerCancellation.IsCancellationRequested) { }
    }

    private Task StartPowerRefresh()
    {
        if (_closeWhenStopped || !_powerWork.IsCompleted) return _powerWork;
        return _powerWork = RefreshPowerAsync();
    }

    private async Task RefreshPowerAsync()
    {
        RefreshPowerButton.IsEnabled = false;
        PowerStatusText.Text = "Atualizando bateria e alimentação…";
        try
        {
            var snapshot = await Task.Run(() => new LinuxPowerCollector(new LinuxFiles()).Collect(_powerCancellation.Token));
            if (_closeWhenStopped) return;
            _powerInventory = snapshot;
            _powerCollectionError = null;
            PowerEntries.ItemsSource = snapshot.Supplies;
            PowerStatusText.Text = $"{snapshot.Supplies.Count} fonte(s)/bateria(s) · atualizado às {snapshot.Timestamp:HH:mm:ss} · atualização automática a cada ~5 s";
            PowerNoticesText.Text = string.Join(Environment.NewLine, snapshot.Notices);
        }
        catch (OperationCanceledException) when (_powerCancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _powerInventory = null;
            _powerCollectionError = ex.Message;
            PowerEntries.ItemsSource = null;
            PowerNoticesText.Text = "";
            PowerStatusText.Text = $"Leitura indisponível: {ex.Message}";
        }
        finally { RefreshPowerButton.IsEnabled = !_closeWhenStopped; }
    }
}
