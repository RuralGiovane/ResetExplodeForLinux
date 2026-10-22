using Avalonia.Interactivity;
using ResetExplode.Hardware;

namespace ResetExplode;

public partial class MainWindow
{
    private StorageInventory? _storageInventory;
    private MountInventory? _mountInventory;
    private DateTimeOffset? _inventoryCollectedAt;
    private Task _storageWork = Task.CompletedTask;
    private readonly CancellationTokenSource _storageCancellation = new();

    private async void RefreshStorage(object? sender, RoutedEventArgs e) => await StartStorageRefresh();

    private Task StartStorageRefresh()
    {
        if (_closeWhenStopped || !_storageWork.IsCompleted) return _storageWork;
        return _storageWork = RefreshStorageAsync();
    }

    private async Task RefreshStorageAsync()
    {
        RefreshStorageButton.IsEnabled = false;
        StorageStatusText.Text = "Lendo dispositivos…";
        try
        {
            var collected = await Task.Run(() =>
            {
                var files = new LinuxFiles();
                var blocks = new LinuxStorageCollector(files).Collect(_storageCancellation.Token);
                var mounts = new LinuxMountCollector(files).Collect(_storageCancellation.Token);
                return (Blocks: blocks, Mounts: mounts);
            });
            var inventory = collected.Blocks;
            if (_closeWhenStopped) return;
            _storageInventory = inventory;
            _mountInventory = collected.Mounts;
            _inventoryCollectedAt = DateTimeOffset.UtcNow;
            StorageEntries.ItemsSource = inventory.Devices;
            SmartDevice.ItemsSource = inventory.Devices.Where(d => d.Parent == null).ToArray();
            SmartDevice.SelectedIndex = -1;
            MountEntries.ItemsSource = collected.Mounts.Mounts;
            StorageStatusText.Text = $"{inventory.Devices.Count} dispositivo(s)/partição(ões) · atualizado às {inventory.Timestamp:HH:mm:ss}";
            StorageNoticesText.Text = string.Join(Environment.NewLine, inventory.Notices.Concat(collected.Mounts.Notices));
        }
        catch (OperationCanceledException) when (_storageCancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _storageInventory = null;
            _mountInventory = null;
            _inventoryCollectedAt = null;
            StorageEntries.ItemsSource = null;
            SmartDevice.ItemsSource = null;
            MountEntries.ItemsSource = null;
            StorageNoticesText.Text = "";
            StorageStatusText.Text = $"Inventário indisponível: {ex.Message}";
        }
        finally { RefreshStorageButton.IsEnabled = !_closeWhenStopped; }
    }
}
