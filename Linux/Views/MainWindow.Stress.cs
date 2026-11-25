using Avalonia.Interactivity;
using ResetExplode.Hardware;

namespace ResetExplode;

public partial class MainWindow
{
    private StressProgress? _stressProgress;
    private double _stressDurationSeconds;

    private async void RunCpuStress(object? sender, RoutedEventArgs e) => await RunStressAsync(true);

    private async void RunRamStress(object? sender, RoutedEventArgs e) => await RunStressAsync(false);

    private async Task RunStressAsync(bool cpu)
    {
        if (_cancellation != null || _closeWhenStopped || _exporting || _cleaningMemory || _clearingHistory ||
            _latencyCancellation != null || _throughputCancellation != null || _dnsCancellation != null) return;
        bool ram = !cpu;
        long bytes = ram ? (long)(StressMemory.Value ?? 256) * StressTest.MiB : 0;
        var duration = TimeSpan.FromMinutes((double)((cpu ? CpuStressMinutes : RamStressMinutes).Value ?? 5));
        _stressDurationSeconds = duration.TotalSeconds;
        (cpu ? CpuStressResultText : RamStressResultText).Text = "Aguardando pré-checagem térmica e de memória…";
        try
        {
            await RunAsync(cpu ? "cpu-stress" : "ram-stress", cpu ? "Stress de CPU" : "Stress de RAM", ct =>
            {
                if (ram)
                {
                    var available = LinuxSystemInfoCollector.ReadMemory(new LinuxFiles()).AvailableBytes;
                    long runtimeBudget = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes - GC.GetTotalMemory(false);
                    StressTest.ValidateMemory(bytes, available, runtimeBudget);
                }
                var result = StressTest.Run(cpu, bytes, duration, p => Volatile.Write(ref _stressProgress, p), ct);
                string memory = !ram ? "RAM não selecionada" : result.MemoryPasses == 0
                    ? "RAM inconclusiva: nenhuma passada completa" : $"RAM: {result.MemoryPasses} passada(s) sem divergência observada";
                return new BenchmarkMeasurement($"Carga encerrada após {result.Seconds:N1} s · {result.CpuThreads} thread(s) CPU · {memory}. Não comprova estabilidade.",
                    null, new() { ["seconds"] = result.Seconds, ["cpuThreads"] = result.CpuThreads,
                        ["allocatedBytes"] = result.AllocatedBytes, ["memoryPasses"] = result.MemoryPasses });
            }, new() { ["stressCpu"] = cpu.ToString(), ["memoryBytes"] = bytes.ToString(),
                ["durationSeconds"] = duration.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                mandatoryThermalLimit: (double)((cpu ? CpuStressLimit : RamStressLimit).Value ?? 90));
        }
        finally
        {
            Volatile.Write(ref _stressProgress, null);
        }
    }
}
