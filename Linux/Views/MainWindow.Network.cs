using Avalonia.Interactivity;
using ResetExplode.Hardware;

namespace ResetExplode;

public partial class MainWindow
{
    private Task _networkWork = Task.CompletedTask;
    private readonly CancellationTokenSource _networkCancellation = new();

    private async void RefreshNetwork(object? sender, RoutedEventArgs e) => await StartNetworkRefresh();

    private Task StartNetworkRefresh()
    {
        if (_closeWhenStopped || !_networkWork.IsCompleted) return _networkWork;
        return _networkWork = RefreshNetworkAsync();
    }

    private async Task RefreshNetworkAsync()
    {
        RefreshNetworkButton.IsEnabled = false;
        NetworkStatusText.Text = "Lendo interfaces de rede…";
        try
        {
            var inventory = await Task.Run(() => new LinuxNetworkCollector(new LinuxFiles()).Collect(_networkCancellation.Token));
            if (_closeWhenStopped) return;
            NetworkEntries.ItemsSource = inventory.Interfaces;
            NetworkDnsText.Text = inventory.Resolver.Description;
            NetworkNoticesText.Text = string.Join(Environment.NewLine, inventory.Notices);
            NetworkStatusText.Text = $"{inventory.Interfaces.Count} interface(s) · atualizado às {inventory.Timestamp:HH:mm:ss}";
        }
        catch (OperationCanceledException) when (_networkCancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            NetworkEntries.ItemsSource = null;
            NetworkDnsText.Text = "Indisponível";
            NetworkNoticesText.Text = "";
            NetworkStatusText.Text = $"Inventário de rede indisponível: {ex.Message}";
        }
        finally { RefreshNetworkButton.IsEnabled = !_closeWhenStopped; }
    }
}
