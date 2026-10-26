using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace ResetExplode;

public partial class MainWindow : Window
{
    private readonly BenchmarkRunner _runner = new();
    private readonly StackPanel _benchmarkRunPanel;
    private ContentControl _runHost;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _thermalStatusTimer;
    private readonly Stopwatch _elapsed = new();
    private readonly WriteableBitmap _bitmap;
    private CancellationTokenSource? _cancellation;
    private BenchRenderer? _renderer;
    private GameSimBench? _simulation;
    private string _currentTest = "";
    private bool _closeWhenStopped;
    private bool _readyToClose;
    private bool _shutdownStarted;

    public MainWindow()
    {
        InitializeComponent();
        _benchmarkRunPanel = BenchmarkRunPanel;
        _runHost = RenderRunHost;
        InitializePersistence();
        MachineText.Text = $"{Environment.MachineName} · {Environment.ProcessorCount} threads lógicas · {RuntimeInformation.OSDescription}";
        _bitmap = new WriteableBitmap(new PixelSize(BenchRenderer.Width, BenchRenderer.Height),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        RenderImage.Source = _bitmap;
        _renderer = new BenchRenderer();
        Array.Fill(_renderer.Pixels, unchecked((int)0xFF000000));
        RefreshBitmap();
        _renderer = null;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) => RefreshProgress();
        _thermalStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _thermalStatusTimer.Tick += (_, _) => UpdateThermalStatus();
        _thermalStatusTimer.Start();
        UpdateThermalStatus();
        Closing += OnClosing;
        Opened += async (_, _) => { StartTelemetry(); StartPowerMonitoring(); await Task.WhenAll(StartStorageRefresh(), StartSerialRefresh(), StartNetworkRefresh()); };
        Closed += (_, _) => { StopTelemetry(); _storageCancellation.Dispose(); _serialCancellation.Dispose(); _networkCancellation.Dispose(); _powerCancellation.Dispose(); _stabilityCancellation.Dispose(); _timer.Stop(); _thermalStatusTimer.Stop(); RenderImage.Source = null; _bitmap.Dispose(); };
    }

    internal void ActivateFromAnotherInstance()
    {
        if (_closeWhenStopped) return;
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        // Respeitar diálogos modais em andamento.
        var dialog = OwnedWindows.FirstOrDefault(window => window.IsVisible);
        (dialog ?? this).Activate();
    }

    private async void RunSingle(object? sender, RoutedEventArgs e) => await RunRenderAsync(1);
    private async void RunMulti(object? sender, RoutedEventArgs e) => await RunRenderAsync(Environment.ProcessorCount);

    private Task RunRenderAsync(int threads)
    {
        if (_cancellation != null || _closeWhenStopped || _exporting || _cleaningMemory || _clearingHistory || (_latencyCancellation != null || _throughputCancellation != null || _dnsCancellation != null)) return Task.CompletedTask;
        _renderer = new BenchRenderer();
        var renderer = _renderer;
        return RunAsync("cpu-render", $"Path tracing · {threads} thread(s)", ct =>
        {
            var result = renderer.Run(threads, TimeSpan.Zero, ct);
            return new BenchmarkMeasurement($"CPU ({threads} thread(s)): {result.Score:N1} pts · {result.Seconds:N1} s · {result.Passes} passada(s)",
                result.Score, new() { ["seconds"] = result.Seconds, ["samples"] = result.Samples, ["passes"] = result.Passes });
        }, new() { ["threads"] = threads.ToString(), ["resolution"] = "960x540", ["samplesPerPixel"] = "1024" });
    }

    private async void RunSimulation(object? sender, RoutedEventArgs e)
    {
        if (_cancellation != null || _closeWhenStopped || _exporting || _cleaningMemory || _clearingHistory || (_latencyCancellation != null || _throughputCancellation != null || _dnsCancellation != null)) return;
        _simulation = new GameSimBench();
        var simulation = _simulation;
        await RunAsync("cpu-simulation", "Simulação de jogo", ct =>
        {
            var result = simulation.Run(TimeSpan.FromSeconds(10), ct);
            return new BenchmarkMeasurement($"Simulação: {result.AvgFps:N1} FPS · 1% low {result.Low1Fps:N1} · {result.Frames} frames",
                result.Score, new() { ["avgFps"] = result.AvgFps, ["low1Fps"] = result.Low1Fps, ["frames"] = result.Frames, ["seconds"] = result.Seconds });
        }, new() { ["entities"] = "30000", ["durationSeconds"] = "10" });
    }

    private async void RunLatency(object? sender, RoutedEventArgs e) =>
        await RunAsync("ram-latency", "Latência da RAM", ct =>
        {
            double ns = MemoryBench.MeasureLatencyNs(ct);
            return new BenchmarkMeasurement($"RAM: {ns:N1} ns", null, new() { ["latencyNs"] = ns });
        });

    private async void RunBandwidth(object? sender, RoutedEventArgs e) =>
        await RunAsync("ram-bandwidth", "Banda da RAM", ct =>
        {
            var result = MemoryBench.MeasureBandwidth(ct);
            return new BenchmarkMeasurement($"RAM: leitura {result.Read:N2} GB/s · cópia {result.Copy:N2} GB/s",
                null, new() { ["readGBs"] = result.Read, ["copyGBs"] = result.Copy });
        });

    private async Task RunAsync(string test, string name, Func<CancellationToken, BenchmarkMeasurement> work,
        Dictionary<string, string>? parameters = null)
    {
        if (_cancellation != null || _closeWhenStopped || _exporting || _cleaningMemory || _clearingHistory || (_latencyCancellation != null || _throughputCancellation != null || _dnsCancellation != null)) return;
        ContentControl destination = test switch
        {
            "cpu-simulation" => SimulationRunHost,
            "ram-latency" or "ram-bandwidth" => MemoryRunHost,
            "disk-direct" or "disk-loading" => DiskRunHost,
            _ => RenderRunHost
        };
        if (_runHost != destination)
        {
            _runHost.Content = null;
            destination.Content = _benchmarkRunPanel;
            _runHost = destination;
        }
        ClearHistoryConfirmation.IsVisible = false;
        bool thermalEnabled = ThermalEnabled.IsChecked == true;
        double thermalLimit = (double)(ThermalLimit.Value ?? (decimal)CpuThermalPolicy.DefaultLimit);
        int started = 0;
        var settings = new LinuxSettings { ThermalEnabled = thermalEnabled, ThermalLimit = thermalLimit };
        var methodology = BenchmarkMethodology.Parameters(test);
        if (parameters != null)
            foreach (var parameter in parameters) methodology[parameter.Key] = parameter.Value;
        var context = BenchmarkContext(methodology);
        BenchmarkHistoryEntry? historyEntry = null;
        BenchmarkHistoryEntry Entry(BenchmarkOutcome outcome, string summary, BenchmarkMeasurement? measured = null) => new()
        {
            Id = Guid.NewGuid(), Date = DateTimeOffset.UtcNow, Test = test, Name = name,
            Outcome = outcome, Summary = summary, Score = measured?.Score,
            Metrics = measured?.Metrics ?? new(), Context = context,
            ThermalEnabled = thermalEnabled, ThermalLimit = thermalLimit
        };
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        _currentTest = name;
        SetRunning(true);
        RunProgress.Value = 0;
        RunProgress.IsIndeterminate = _renderer == null && _simulation == null;
        try
        {
            StatusText.Text = "Salvando configuração…";
            await QueuePersistenceAsync(settings);
            cancellation.Token.ThrowIfCancellationRequested();
            _elapsed.Restart();
            _timer.Start();
            RefreshProgress();
            BenchmarkMeasurement result;
            string mode = thermalEnabled ? $"monitoramento CPU: {thermalLimit:N0} °C" : "sem interrupção térmica";
            BenchmarkMeasurement Execute(CancellationToken ct)
            {
                Interlocked.Exchange(ref started, 1);
                return work(ct);
            }
            if (thermalEnabled)
                result = await _runner.RunThermalAsync(Execute, cancellation.Token,
                    new CpuThermalPolicy(thermalLimit), () => Volatile.Read(ref _thermalReading));
            else
                result = await _runner.RunAsync(Execute, cancellation.Token);
            RefreshBitmap();
            historyEntry = Entry(BenchmarkOutcome.Completed, $"{result.Summary} · {mode}", result);
            ResultsText.Text = $"{DateTime.Now:HH:mm:ss} · {historyEntry.Summary}\n{ResultsText.Text}";
            StatusText.Text = "Teste concluído.";
            RunProgress.Value = 100;
        }
        catch (ThermalStopException ex)
        {
            string state = Volatile.Read(ref started) == 0 ? "NÃO INICIADO" : "INTERROMPIDO";
            StatusText.Text = $"{state}: {ex.Stop.Message} Sem pontuação.";
            historyEntry = Entry(Volatile.Read(ref started) == 0 ? BenchmarkOutcome.NotStarted : BenchmarkOutcome.ThermalStopped,
                StatusText.Text);
            ResultsText.Text = $"{DateTime.Now:HH:mm:ss} · {name} · {StatusText.Text}\n{ResultsText.Text}";
            RunProgress.Value = 0;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = _closeWhenStopped ? "Cancelado ao fechar a janela. Sem pontuação." : "Cancelado pelo usuário. Sem pontuação.";
            historyEntry = Entry(BenchmarkOutcome.Cancelled, StatusText.Text);
            ResultsText.Text = $"{DateTime.Now:HH:mm:ss} · {name} · {StatusText.Text}\n{ResultsText.Text}";
            RunProgress.Value = 0;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Não foi possível concluir o teste: {ex.Message}";
            historyEntry = Entry(BenchmarkOutcome.Failed, StatusText.Text);
            RunProgress.Value = 0;
        }
        finally
        {
            _timer.Stop();
            _elapsed.Stop();
            if (historyEntry != null) await RecordBenchmarkAsync(historyEntry, settings);
            RunProgress.IsIndeterminate = false;
            _renderer = null;
            _simulation = null;
            _cancellation = null;
            SetRunning(false);
            UpdateThermalStatus();
            if (_closeWhenStopped) await FinishCloseAsync();
        }
    }

    private void SetRunning(bool running)
    {
        SingleButton.IsEnabled = MultiButton.IsEnabled = SimButton.IsEnabled =
            DiskBenchmarkButton.IsEnabled = LoadingBenchmarkButton.IsEnabled = DiskBenchmarkPath.IsEnabled =
            LatencyButton.IsEnabled = BandwidthButton.IsEnabled = !running && !_exporting && !_cleaningMemory && !_clearingHistory && _latencyCancellation == null && _throughputCancellation == null && _dnsCancellation == null;
        StartNetworkTestButton.IsEnabled = !running && !_exporting && !_cleaningMemory && !_clearingHistory && _latencyCancellation == null && _throughputCancellation == null && _dnsCancellation == null && !_closeWhenStopped;
        StartDnsButton.IsEnabled = StartNetworkTestButton.IsEnabled;
        StartThroughputButton.IsEnabled = StartNetworkTestButton.IsEnabled;
        ReleaseMemoryButton.IsEnabled = !running && !_cleaningMemory && !_clearingHistory && !_exporting && _latencyCancellation == null && _throughputCancellation == null && _dnsCancellation == null && !_closeWhenStopped;
        ClearSessionButton.IsEnabled = ClearHistoryButton.IsEnabled = ConfirmClearHistoryButton.IsEnabled = ReleaseMemoryButton.IsEnabled;
        CancelButton.IsEnabled = running;
        ThermalEnabled.IsEnabled = ThermalLimit.IsEnabled = !running && !_exporting && !_cleaningMemory && !_clearingHistory && _latencyCancellation == null && _throughputCancellation == null && _dnsCancellation == null;
        SaveSettingsButton.IsEnabled = RetrySaveButton.IsEnabled = !running && !_exporting && !_cleaningMemory && !_clearingHistory && _latencyCancellation == null && _throughputCancellation == null && _dnsCancellation == null;
        ExportHtmlButton.IsEnabled = ExportJsonButton.IsEnabled =
            StorageHtmlButton.IsEnabled = StorageJsonButton.IsEnabled =
            StabilityHtmlButton.IsEnabled = StabilityJsonButton.IsEnabled =
            PowerHtmlButton.IsEnabled = PowerJsonButton.IsEnabled =
            NetworkHtmlButton.IsEnabled = NetworkJsonButton.IsEnabled =
            SerialHtmlButton.IsEnabled = SerialJsonButton.IsEnabled = !running && !_exporting && !_cleaningMemory && !_clearingHistory && _latencyCancellation == null && _throughputCancellation == null && _dnsCancellation == null;
    }

    private void UpdateThermalStatus()
    {
        if (ThermalEnabled.IsChecked != true)
        {
            ThermalStateText.Text = "Desativado: os testes não serão interrompidos por temperatura ou perda de leitura.";
            return;
        }
        double limit = (double)(ThermalLimit.Value ?? (decimal)CpuThermalPolicy.DefaultLimit);
        var policy = new CpuThermalPolicy(limit);
        var reading = Volatile.Read(ref _thermalReading);
        var stop = policy.Evaluate(reading);
        ThermalStateText.Text = stop != null ? stop.Message
            : $"CPU: {reading!.Celsius:N1} °C · limite: {limit:N0} °C · leitura recente disponível.";
    }

    private void CancelRun(object? sender, RoutedEventArgs e) => RequestCancellation();

    private void RequestCancellation()
    {
        _cancellation?.Cancel();
        CancelButton.IsEnabled = false;
        StatusText.Text = "Cancelando; aguardando o trabalho em andamento terminar…";
    }

    private void RefreshProgress()
    {
        if (_cancellation?.IsCancellationRequested == true) return;
        StatusText.Text = $"{_currentTest} · {_elapsed.Elapsed.TotalSeconds:N0} s";
        var disk = Volatile.Read(ref _diskProgress);
        if (disk != null)
        {
            StatusText.Text += $" · {disk.Phase}";
            RunProgress.IsIndeterminate = false;
            RunProgress.Value = 100 * disk.Fraction;
        }
        else if (_renderer != null)
        {
            RefreshBitmap();
            RunProgress.Value = 100.0 * _renderer.BucketsDone / Math.Max(1, _renderer.BucketsTotal);
        }
        else if (_simulation != null)
        {
            RunProgress.Value = 100 * _simulation.Progress;
            StatusText.Text += _elapsed.Elapsed.TotalSeconds < 2 ? " · aquecimento" : $" · {_simulation.CurrentFps:N1} FPS";
        }
    }

    private void RefreshBitmap()
    {
        if (_renderer == null) return;
        using var framebuffer = _bitmap.Lock();
        // O stride pode ser maior que a largura em bytes, conforme o backend.
        for (int y = 0; y < BenchRenderer.Height; y++)
            Marshal.Copy(_renderer.Pixels, y * BenchRenderer.Width,
                IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes), BenchRenderer.Width);
        RenderImage.InvalidateVisual();
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_readyToClose) return;
        e.Cancel = true;
        _closeWhenStopped = true;
        if (_cancellation != null) RequestCancellation();
        else await FinishCloseAsync();
    }

    private async Task FinishCloseAsync()
    {
        if (_shutdownStarted) return;
        _shutdownStarted = true;
        StopTelemetry();
        _storageCancellation.Cancel();
        _serialCancellation.Cancel();
        _networkCancellation.Cancel();
        _powerCancellation.Cancel();
        _stabilityCancellation.Cancel();
        await _powerLoop;
        await _powerWork;
        await _stabilityWork;
        await _stabilitySessionWork;
        await _kernelLogWork;
        await _storageWork;
        await _serialWork;
        await _networkWork;
        await _latencyWork;
        await _throughputWork;
        await _dnsWork;
        await _smartWork;
        // A UI continua processando os callbacks pendentes até a coleta liberar a NVML.
        await _telemetryTask;
        await _persistenceWork;
        await _exportWork;
        await _memoryCleanupWork;
        await _historyClearWork;
        _readyToClose = true;
        Close();
    }
}
