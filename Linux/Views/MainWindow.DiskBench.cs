using Avalonia.Interactivity;

namespace ResetExplode;

public partial class MainWindow
{
    private DiskProgress? _diskProgress;
    private async void RunDiskBenchmark(object? sender, RoutedEventArgs e) => await RunDiskWorkAsync(false);
    private async void RunLoadingBenchmark(object? sender, RoutedEventArgs e) => await RunDiskWorkAsync(true);
    private async Task RunDiskWorkAsync(bool loading)
    {
        if (_cancellation != null || _closeWhenStopped || _exporting || _cleaningMemory || _clearingHistory || _latencyCancellation != null || _throughputCancellation != null || _dnsCancellation != null) return;
        string path = DiskBenchmarkPath.Text?.Trim() ?? "";
        try
        {
            await RunAsync(loading ? "disk-loading" : "disk-direct", loading ? "Carregamento de arquivos" : "Benchmark de disco",
                ct => loading ? DiskBenchmarks.RunLoading(path, p => Volatile.Write(ref _diskProgress, p), ct)
                    : DiskBenchmarks.RunDisk(path, p => Volatile.Write(ref _diskProgress, p), ct),
                new() { ["directory"] = path });
        }
        finally { Volatile.Write(ref _diskProgress, null); }
    }
}
