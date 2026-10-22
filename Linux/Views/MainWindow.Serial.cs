using Avalonia.Interactivity;
using ResetExplode.Hardware;

namespace ResetExplode;

public partial class MainWindow
{
    private SerialInventory? _serialInventory;
    private string? _serialCollectionError;
    private Task _serialWork = Task.CompletedTask;
    private readonly CancellationTokenSource _serialCancellation = new();

    private async void RefreshSerial(object? sender, RoutedEventArgs e) => await StartSerialRefresh();

    private Task StartSerialRefresh()
    {
        if (_closeWhenStopped || !_serialWork.IsCompleted) return _serialWork;
        return _serialWork = RefreshSerialAsync();
    }

    private async Task RefreshSerialAsync()
    {
        RefreshSerialButton.IsEnabled = false;
        SerialStatusText.Text = "Lendo portas seriais…";
        try
        {
            var inventory = await Task.Run(() => new LinuxSerialCollector(new LinuxFiles()).Collect(_serialCancellation.Token));
            if (_closeWhenStopped) return;
            _serialInventory = inventory;
            _serialCollectionError = null;
            SerialEntries.ItemsSource = inventory.Ports;
            SerialGroupsText.Text = string.Join(Environment.NewLine, inventory.Groups.Select(g => g.Description));
            SerialNoticesText.Text = string.Join(Environment.NewLine, inventory.Notices);
            SerialStatusText.Text = $"{inventory.Ports.Count} porta(s) · atualizado às {inventory.Timestamp:HH:mm:ss}";
        }
        catch (OperationCanceledException) when (_serialCancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _serialInventory = null;
            _serialCollectionError = ex.Message;
            SerialEntries.ItemsSource = null;
            SerialGroupsText.Text = "";
            SerialNoticesText.Text = "";
            SerialStatusText.Text = $"Inventário serial indisponível: {ex.Message}";
        }
        finally { RefreshSerialButton.IsEnabled = !_closeWhenStopped; }
    }
}
