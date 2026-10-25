using Avalonia.Interactivity;
using ResetExplode.Hardware;

namespace ResetExplode;

public partial class MainWindow
{
    private NetworkInventory? _networkInventory;
    private string? _networkCollectionError;
    private NetworkLatencyResult? _networkLatencyResult;
    private DateTimeOffset? _networkLatencyCompletedAt;
    private string _networkLatencyStatus = "Nenhum teste executado.";
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
            _networkInventory = inventory;
            _networkCollectionError = null;
            NetworkEntries.ItemsSource = inventory.Interfaces;
            NetworkDnsText.Text = inventory.Resolver.Description;
            NetworkNoticesText.Text = string.Join(Environment.NewLine, inventory.Notices);
            NetworkStatusText.Text = $"{inventory.Interfaces.Count} interface(s) · atualizado às {inventory.Timestamp:HH:mm:ss}";
        }
        catch (OperationCanceledException) when (_networkCancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _networkInventory = null;
            _networkCollectionError = ex.Message;
            NetworkEntries.ItemsSource = null;
            NetworkDnsText.Text = "Indisponível";
            NetworkNoticesText.Text = "";
            NetworkStatusText.Text = $"Inventário de rede indisponível: {ex.Message}";
        }
        finally { RefreshNetworkButton.IsEnabled = !_closeWhenStopped; }
    }
    private Task _latencyWork = Task.CompletedTask;
    private CancellationTokenSource? _latencyCancellation;

    private async void StartLatencyTest(object? sender, RoutedEventArgs e)
    {
        if (_throughputCancellation != null || _dnsCancellation != null || _closeWhenStopped || !_latencyWork.IsCompleted || _cancellation != null || _exporting || _cleaningMemory || _clearingHistory) return;
        _latencyWork = RunLatencyTestAsync();
        await _latencyWork;
    }

    private void CancelLatencyTest(object? sender, RoutedEventArgs e) => _latencyCancellation?.Cancel();

    private async Task RunLatencyTestAsync()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_networkCancellation.Token);
        _latencyCancellation = cancellation;
        _networkLatencyResult = null;
        _networkLatencyCompletedAt = null;
        _networkLatencyStatus = "Teste em andamento.";
        SetRunning(false);
        LatencyTarget.IsEnabled = false;
        CancelNetworkTestButton.IsEnabled = true;
        NetworkTestResult.Text = "Executando 10 tentativas… (até aproximadamente 25 segundos)";
        try
        {
            var target = LatencyTarget.Text ?? "";
            var result = await Task.Run(() => new NetworkLatencyTest().RunAsync(target, cancellation.Token));
            if (_closeWhenStopped) return;
            _networkLatencyResult = result;
            _networkLatencyCompletedAt = DateTimeOffset.Now;
            _networkLatencyStatus = "Teste concluído.";
            static string Ms(double? value) => value.HasValue ? $"{value.Value:N1} ms" : "Indisponível";
            NetworkTestResult.Text = $"Destino: {result.Target} · {result.Received}/{result.Probes.Count} respostas · perda ICMP: {result.LossPercent:N1}%\n" +
                $"Latência mín./média/máx.: {Ms(result.MinimumMs)} / {Ms(result.AverageMs)} / {Ms(result.MaximumMs)}\n" +
                $"Jitter: {Ms(result.JitterMs)}\n" +
                string.Join(" · ", result.Probes.GroupBy(p => p.Status).Select(g => $"{g.Key}: {g.Count()}"));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            NetworkTestResult.Text = _networkLatencyStatus = "Teste cancelado. Nenhum resultado parcial publicado.";
        }
        catch (Exception ex)
        {
            NetworkTestResult.Text = _networkLatencyStatus = $"Teste indisponível: {ex.GetBaseException().Message}. Não foi calculada perda de pacotes.";
        }
        finally
        {
            _latencyCancellation = null;
            LatencyTarget.IsEnabled = !_closeWhenStopped;
            CancelNetworkTestButton.IsEnabled = false;
            SetRunning(false);
        }
    }
}
