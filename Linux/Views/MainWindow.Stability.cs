using Avalonia.Interactivity;
using ResetExplode.Hardware;

namespace ResetExplode;

public partial class MainWindow
{
    private EdacSnapshot? _stabilityEdacSnapshot;
    private KernelLogSnapshot? _stabilityLogSnapshot;
    private StabilitySample? _stabilityCurrent;
    private KernelLogSnapshot? _stabilitySessionLogs;
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
        _stabilityEdacSnapshot = null;
        try
        {
            var snapshot = await Task.Run(() => new LinuxEdacCollector(new LinuxFiles()).Collect(_stabilityCancellation.Token));
            if (_closeWhenStopped) return;
            _stabilityEdacSnapshot = snapshot;
            StabilityStatusText.Text = $"{snapshot.Status} · {snapshot.Timestamp:HH:mm:ss}";
            StabilityDetailsText.Text = string.Join(Environment.NewLine + Environment.NewLine, snapshot.Controllers.Select(c => c.Description));
        }
        catch (OperationCanceledException) when (_stabilityCancellation.IsCancellationRequested) { }
        catch (Exception ex) { if (!_closeWhenStopped) StabilityStatusText.Text = $"EDAC indisponível: {ex.Message}"; }
        finally { RefreshStabilityButton.IsEnabled = !_closeWhenStopped; }
    }

    private Task _kernelLogWork = Task.CompletedTask;
    private CancellationTokenSource? _kernelLogCancellation;

    private async void RefreshKernelLogs(object? sender, RoutedEventArgs e)
    {
        if (_closeWhenStopped || !_kernelLogWork.IsCompleted) return;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stabilityCancellation.Token);
        _kernelLogCancellation = cancellation;
        _kernelLogWork = RefreshKernelLogsAsync(cancellation.Token);
        try { await _kernelLogWork; }
        finally { _kernelLogCancellation = null; }
    }

    private void CancelKernelLogs(object? sender, RoutedEventArgs e) => _kernelLogCancellation?.Cancel();

    private async Task RefreshKernelLogsAsync(CancellationToken cancellation)
    {
        RefreshKernelLogsButton.IsEnabled = false;
        CancelKernelLogsButton.IsEnabled = true;
        KernelLogStatusText.Text = "Consultando avisos e erros do kernel…";
        KernelLogDetailsText.Text = "";
        _stabilityLogSnapshot = null;
        try
        {
            var snapshot = await Task.Run(() => new LinuxKernelLogReader().ReadAsync(cancellation), cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (_closeWhenStopped) return;
            _stabilityLogSnapshot = snapshot;
            KernelLogStatusText.Text = $"{snapshot.Status} · Consulta: {snapshot.Timestamp.ToLocalTime():HH:mm:ss}";
            KernelLogDetailsText.Text = KernelLogClassifier.Summarize(snapshot.Entries) + Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine + Environment.NewLine,
                snapshot.Entries.Select(entry => entry.Description));
        }
        catch (OperationCanceledException)
        { if (!_closeWhenStopped) KernelLogStatusText.Text = "Consulta cancelada. Nenhum resultado publicado."; }
        catch (Exception ex)
        { if (!_closeWhenStopped) KernelLogStatusText.Text = $"Logs indisponíveis: {ex.Message}"; }
        finally
        {
            RefreshKernelLogsButton.IsEnabled = !_closeWhenStopped;
            CancelKernelLogsButton.IsEnabled = false;
        }
    }

    private StabilitySample? _stabilityBaseline;
    private Task _stabilitySessionWork = Task.CompletedTask;
    private CancellationTokenSource? _stabilitySessionCancellation;

    private async void StartStabilitySession(object? sender, RoutedEventArgs e) => await RunStabilitySessionAsync(true);
    private async void CompareStabilitySession(object? sender, RoutedEventArgs e) => await RunStabilitySessionAsync(false);
    private void CancelStabilitySession(object? sender, RoutedEventArgs e) => _stabilitySessionCancellation?.Cancel();

    private async Task RunStabilitySessionAsync(bool start)
    {
        if (_closeWhenStopped || !_stabilitySessionWork.IsCompleted || (!start && _stabilityBaseline == null)) return;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stabilityCancellation.Token);
        _stabilitySessionCancellation = cancellation;
        _stabilitySessionWork = UpdateStabilitySessionAsync(start, cancellation.Token);
        try { await _stabilitySessionWork; }
        finally { _stabilitySessionCancellation = null; }
    }

    private async Task UpdateStabilitySessionAsync(bool start, CancellationToken cancellation)
    {
        StartStabilitySessionButton.IsEnabled = false;
        CompareStabilitySessionButton.IsEnabled = false;
        CancelStabilitySessionButton.IsEnabled = true;
        StabilitySessionDetailsText.Text = "";
        _stabilityCurrent = null;
        _stabilitySessionLogs = null;
        StabilitySessionStatusText.Text = start ? "Criando referência…" : "Comparando com a referência…";
        if (start) _stabilityBaseline = null;
        try
        {
            var sample = await Task.Run(() => StabilitySession.Capture(cancellation), cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (_closeWhenStopped) return;
            if (start)
            {
                if (sample.BootId == null) throw new InvalidOperationException("Identificação da inicialização indisponível.");
                _stabilityBaseline = sample;
                StabilitySessionStatusText.Text = $"Referência criada em {sample.Edac.Timestamp.ToLocalTime():dd/MM/yyyy HH:mm:ss}. Use COMPARAR quando desejar.";
                StabilitySessionDetailsText.Text = sample.Edac.Status + "\nA referência permanece até criar outra ou fechar o app.";
            }
            else
            {
                var logs = await Task.Run(() => new LinuxKernelLogReader().ReadAsync(cancellation), cancellation);
                var comparison = StabilitySession.Compare(_stabilityBaseline!, sample, logs);
                cancellation.ThrowIfCancellationRequested();
                if (_closeWhenStopped) return;
                _stabilityCurrent = sample;
                _stabilitySessionLogs = logs;
                StabilitySessionStatusText.Text = "Comparação concluída. Consulte disponibilidade e limites abaixo.";
                StabilitySessionDetailsText.Text = comparison.Description;
            }
        }
        catch (OperationCanceledException)
        { if (!_closeWhenStopped) StabilitySessionStatusText.Text = "Operação cancelada; nenhum resultado novo publicado."; }
        catch (Exception ex)
        { if (!_closeWhenStopped) StabilitySessionStatusText.Text = $"Comparação indisponível: {ex.Message}"; }
        finally
        {
            StartStabilitySessionButton.IsEnabled = !_closeWhenStopped;
            CompareStabilitySessionButton.IsEnabled = !_closeWhenStopped && _stabilityBaseline != null;
            CancelStabilitySessionButton.IsEnabled = false;
        }
    }
}
