using Avalonia.Interactivity;
using ResetExplode.Hardware;

namespace ResetExplode;

public partial class MainWindow
{
    private Task _throughputWork = Task.CompletedTask;
    private CancellationTokenSource? _throughputCancellation;
    private NetworkThroughputResult? _networkThroughputResult;
    private string _networkThroughputStatus = "Nenhum teste de transferência executado.";

    private async void StartThroughputTest(object? sender, RoutedEventArgs e)
    {
        if (_dnsCancellation != null || _closeWhenStopped || !_throughputWork.IsCompleted || _latencyCancellation != null || _cancellation != null || _exporting || _cleaningMemory || _clearingHistory) return;
        _throughputWork = RunThroughputTestAsync();
        await _throughputWork;
    }

    private void CancelThroughputTest(object? sender, RoutedEventArgs e) => _throughputCancellation?.Cancel();

    private async Task RunThroughputTestAsync()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_networkCancellation.Token);
        _throughputCancellation = cancellation;
        _networkThroughputResult = null;
        ThroughputResultText.Text = _networkThroughputStatus = "Transferindo com speed.cloudflare.com… download e depois upload (até 40 s).";
        SetRunning(false);
        CancelThroughputButton.IsEnabled = true;
        try
        {
            using var http = NetworkThroughputTest.CreateClient();
            var result = await Task.Run(() => new NetworkThroughputTest(http).RunAsync(cancellation.Token));
            cancellation.Token.ThrowIfCancellationRequested();
            if (_closeWhenStopped) return;
            _networkThroughputResult = result;
            _networkThroughputStatus = "Teste de transferência concluído.";
            ThroughputResultText.Text = $"Download: {result.Download.MegabitsPerSecond:N2} Mbit/s · {result.Download.Bytes:N0} bytes em {result.Download.Seconds:N3} s\n" +
                $"Upload: {result.Upload.MegabitsPerSecond:N2} Mbit/s · {result.Upload.Bytes:N0} bytes em {result.Upload.Seconds:N3} s\n" +
                $"{result.Server} · concluído às {result.CompletedAt.ToLocalTime():HH:mm:ss}";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            ThroughputResultText.Text = _networkThroughputStatus = "Teste de transferência cancelado. Nenhum resultado parcial publicado.";
        }
        catch (Exception ex)
        {
            ThroughputResultText.Text = _networkThroughputStatus = $"Transferência indisponível: {ex.GetBaseException().Message}. Sem resultado de velocidade.";
        }
        finally
        {
            _throughputCancellation = null;
            CancelThroughputButton.IsEnabled = false;
            SetRunning(false);
        }
    }
}
