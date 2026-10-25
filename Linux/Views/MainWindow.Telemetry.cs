using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ResetExplode.Hardware;

namespace ResetExplode;

public partial class MainWindow
{
    private CancellationTokenSource? _telemetryCancellation;
    private bool _telemetryStopped;
    private Task _telemetryTask = Task.CompletedTask;
    private TelemetrySnapshot? _snapshot;
    private CpuTemperatureReading? _thermalReading;

    private void ShowSystem(object? sender, RoutedEventArgs e) => SelectSystemPage(true);
    private void ShowBench(object? sender, RoutedEventArgs e) => SelectSystemPage(false);

    private void SelectSystemPage(bool system)
    {
        SystemPage.IsVisible = system;
        BenchPage.IsVisible = !system;
        SystemNav.Classes.Set("primary", system);
        SystemNav.Classes.Set("outline", !system);
        BenchNav.Classes.Set("primary", !system);
        BenchNav.Classes.Set("outline", system);
    }

    private void StartTelemetry()
    {
        if (_telemetryCancellation != null || _telemetryStopped) return;
        var cancellation = new CancellationTokenSource();
        _telemetryCancellation = cancellation;
        // Um único loop: nunca sobrepor consultas, mesmo quando um driver responde lentamente.
        _telemetryTask = Task.Run(async () =>
        {
            var token = cancellation.Token;
            try
            {
                var files = new LinuxFiles();
                using var monitor = new LinuxSensorMonitor(files, new LinuxNvml());
                var system = new LinuxSystemInfoCollector(files).Collect();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!_telemetryStopped)
                    {
                        SystemEntries.ItemsSource = system.Entries;
                        ApplySystemOverview(system);
                        _cpuDescription = system.Entries.FirstOrDefault(e => e.Name == "Processador")?.Value ?? "Indisponível";
                    }
                });
                while (!token.IsCancellationRequested)
                {
                    var snapshot = monitor.Read(token);
                    // Publicar antes de aguardar a UI; o watchdog não depende do dispatcher.
                    Volatile.Write(ref _thermalReading,
                        new CpuTemperatureReading(snapshot.CpuTemperature, snapshot.CollectionStartedTimestamp));
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (!_telemetryStopped) ApplySnapshot(snapshot);
                    });
                    await Task.Delay(TimeSpan.FromSeconds(1), token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                Volatile.Write(ref _thermalReading, null);
                if (!token.IsCancellationRequested)
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (!_telemetryStopped)
                        {
                            _snapshot = null;
                            OverviewGpuText.Text = "Telemetria indisponível";
                            if (OverviewEntries.ItemsSource == null) OverviewMachineText.Text = "Resumo do sistema indisponível";
                            SensorEntries.ItemsSource = null;
                            SensorCountText.Text = "Leituras indisponíveis.";
                            TelemetryNoticesText.Text = "";
                            CpuSummaryText.Text = SidebarCpuText.Text = "CPU · indisponível";
                            RamSummaryText.Text = SidebarRamText.Text = "RAM · indisponível";
                            TelemetryStatusText.Text = $"Coleta interrompida: {ex.Message}";
                        }
                    });
            }
            finally { Volatile.Write(ref _thermalReading, null); cancellation.Dispose(); }
        });
    }

    private void StopTelemetry()
    {
        _telemetryStopped = true;
        Volatile.Write(ref _thermalReading, null);
        // O loop pode já ter terminado por falha; nesse caso seu CTS já foi descartado.
        try { _telemetryCancellation?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private void ApplySnapshot(TelemetrySnapshot snapshot)
    {
        _snapshot = snapshot;
        ApplyGpuOverview(snapshot);
        string load = snapshot.CpuLoadPercent is double value ? $"{value:N1}%" : "indisponível";
        string temperature = snapshot.CpuTemperature is double temp ? $"{temp:N1} °C" : "temperatura indisponível";
        CpuSummaryText.Text = $"CPU · {load} · {temperature}";
        SidebarCpuText.Text = $"CPU · {load}\n{temperature}";
        if (snapshot.Memory.UsedBytes is long used && snapshot.Memory.TotalBytes is long total)
        {
            string ram = $"{used / 1073741824.0:N1} / {total / 1073741824.0:N1} GiB";
            RamSummaryText.Text = $"RAM · {ram} · disponível {snapshot.Memory.AvailableBytes!.Value / 1073741824.0:N1} GiB";
            SidebarRamText.Text = $"RAM · {ram}";
        }
        else RamSummaryText.Text = SidebarRamText.Text = "RAM · leitura indisponível";
        TelemetryStatusText.Text = $"Atualizado às {snapshot.Timestamp:HH:mm:ss} · leituras sequenciais a cada ~1 s";
        TelemetryNoticesText.Text = string.Join(Environment.NewLine, snapshot.Notices);
        ApplySensorFilter();
    }

    private void FilterSensors(object? sender, TextChangedEventArgs e)
    {
        if (_snapshot != null) ApplySensorFilter();
    }

    private void ApplySensorFilter()
    {
        if (_snapshot == null) return;
        string search = SensorSearch.Text?.Trim() ?? "";
        var rows = _snapshot.Sensors.Where(s => search.Length == 0 ||
            s.Hardware.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            s.Label.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        SensorEntries.ItemsSource = rows;
        SensorCountText.Text = $"{rows.Length} de {_snapshot.Sensors.Count} leituras · {rows.Count(s => s.Value != null)} disponíveis";
    }
}
