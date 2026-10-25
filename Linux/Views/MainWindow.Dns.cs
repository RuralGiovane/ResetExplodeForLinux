using Avalonia.Interactivity;
using ResetExplode.Hardware;

namespace ResetExplode;

public partial class MainWindow
{
    private Task _dnsWork = Task.CompletedTask;
    private CancellationTokenSource? _dnsCancellation;
    private NetworkDnsResult? _networkDnsResult;
    private string _networkDnsStatus = "Nenhuma resolução executada.";

    private async void StartDnsTest(object? sender, RoutedEventArgs e)
    {
        if (_closeWhenStopped || !_dnsWork.IsCompleted || _latencyCancellation != null || _throughputCancellation != null || _cancellation != null || _exporting) return;
        _dnsWork = RunDnsTestAsync();
        await _dnsWork;
    }
    private void CancelDnsTest(object? sender, RoutedEventArgs e) => _dnsCancellation?.Cancel();

    private async Task RunDnsTestAsync()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_networkCancellation.Token);
        _dnsCancellation = cancellation;
        _networkDnsResult = null;
        DnsResultText.Text = _networkDnsStatus = "Resolvendo nome pelo sistema…";
        DnsTarget.IsEnabled = false;
        SetRunning(false);
        CancelDnsButton.IsEnabled = true;
        try
        {
            string target = DnsTarget.Text ?? "";
            var result = await Task.Run(() => new NetworkDnsTest().RunAsync(target, cancellation.Token));
            cancellation.Token.ThrowIfCancellationRequested();
            if (_closeWhenStopped) return;
            _networkDnsResult = result;
            _networkDnsStatus = result.Addresses.Count == 0 ? "Resolução concluída sem endereços." : "Resolução concluída.";
            DnsResultText.Text = $"{result.Host} · {result.ElapsedMilliseconds:N2} ms · {result.CompletedAt.ToLocalTime():HH:mm:ss}\n" +
                (result.Addresses.Count == 0 ? "Nenhum endereço retornado." : string.Join("\n", result.Addresses));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            DnsResultText.Text = _networkDnsStatus = "Resolução cancelada. Nenhum resultado publicado.";
        }
        catch (Exception ex)
        {
            DnsResultText.Text = _networkDnsStatus = $"Resolução indisponível: {ex.GetBaseException().Message}";
        }
        finally
        {
            _dnsCancellation = null;
            DnsTarget.IsEnabled = !_closeWhenStopped;
            CancelDnsButton.IsEnabled = false;
            SetRunning(false);
        }
    }
}
