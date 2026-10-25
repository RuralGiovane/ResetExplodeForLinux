using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
        SmartReadButton.IsEnabled = SmartDevice.IsEnabled = false;
        try
        {
            if (!await ConfirmSmartPermissionAsync(device))
            {
                if (!_closeWhenStopped) SmartResultText.Text = "Consulta SMART cancelada.";
                return;
            }
            if (_closeWhenStopped) return;
            _lastSmartReport = null;
            _smartCollectedAt = null;
            SmartResultText.Text = $"Aguardando autorização para consultar {device.Title}…";
            var result = await LinuxSmartReader.CreatePrivileged().ReadAsync(device.Name, _storageCancellation.Token);
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

    private async Task<bool> ConfirmSmartPermissionAsync(BlockDevice device)
    {
        var dialog = new Window
        {
            Title = "Permissão para consultar SMART", Width = 480,
            SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var cancel = new Button { Content = "Cancelar", IsCancel = true };
        var proceed = new Button { Content = "Prosseguir" };
        cancel.Click += (_, _) => dialog.Close(false);
        proceed.Click += (_, _) => dialog.Close(true);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24), Spacing = 20,
            Children =
            {
                new TextBlock
                {
                    Text = $"Ao prosseguir, você autorizará uma consulta com permissão de administrador para verificar os dados SMART de /dev/{device.Name}. O sistema poderá solicitar sua senha.\n\nA consulta apenas lê os dados do disco e pode acordá-lo se estiver em repouso. A permissão se aplica à consulta; o aplicativo continua com seu usuário normal.",
                    TextWrapping = TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 12, Children = { cancel, proceed }
                }
            }
        };
        dialog.Opened += (_, _) => cancel.Focus();
        using var registration = _storageCancellation.Token.Register(() =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => dialog.Close(false)));
        return await dialog.ShowDialog<bool>(this);
    }

}
