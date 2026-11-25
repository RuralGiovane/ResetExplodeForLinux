using System.Diagnostics;
using ResetExplode.Hardware;

internal static class GpuThermalChecks
{
    public static void Run()
    {
        GpuSelectionChecks.Run();
        GpuPreviewChecks.Run();
        const string pci = "0000:01:00.0";
        var guard = new GpuThermalGuard(pci, 80);
        GpuTemperature Reading(double? value = 50, string? error = null) => new(pci, value, Stopwatch.GetTimestamp(), error);
        void Refuse(Action action)
        {
            try { action(); }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { return; }
            throw new Exception("Proteção GPU aceitou condição inválida.");
        }
        guard.Check(Reading());
        guard.Check(Reading(79.9));
        foreach (double? value in new double?[] { null, double.NaN, double.PositiveInfinity, -1, 80, 120, 151 })
            Refuse(() => guard.Check(Reading(value)));
        Refuse(() => guard.Check(Reading() with { PciAddress = "0000:00:02.0" }));
        Refuse(() => guard.Check(Reading(error: "Sensor falhou")));
        Refuse(() => guard.Check(Reading() with { StartedTimestamp = Stopwatch.GetTimestamp() - 3 * Stopwatch.Frequency }));
        Refuse(() => guard.Check(Reading() with { StartedTimestamp = Stopwatch.GetTimestamp() + Stopwatch.Frequency }));
        Refuse(() => guard.Check(Reading() with { StartedTimestamp = 0 }));
        foreach (string address in new[] { "", "GPU1", "0000:01:20.0", "0000:01:00.8", "0000:01:00.0\n" })
            Refuse(() => new GpuThermalGuard(address, 80));
        foreach (double limit in new[] { double.NaN, double.PositiveInfinity, 59, 91 })
            Refuse(() => new GpuThermalGuard(pci, limit));
        Refuse(() => VulkanGpuRenderer.Run(pci, TimeSpan.Zero));
        Refuse(() => VulkanGpuRenderer.Run(pci, TimeSpan.FromMinutes(6)));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { VulkanGpuRenderer.Run(pci, TimeSpan.FromSeconds(1), cancellation: cancellation.Token); throw new Exception("Pré-cancelamento ignorado."); }
        catch (OperationCanceledException) { }
        Console.WriteLine("OK: proteção GPU por PCI, ausência/falha/idade do sensor, limite térmico, parâmetros e pré-cancelamento sem carga real.");
    }
}
