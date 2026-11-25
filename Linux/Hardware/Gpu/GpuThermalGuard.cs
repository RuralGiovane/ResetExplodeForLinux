using System.Diagnostics;

namespace ResetExplode.Hardware;

public sealed record GpuTemperature(string PciAddress, double? Celsius, long StartedTimestamp, string? Error);

public sealed class GpuThermalException(string message) : InvalidOperationException(message)
{
    public bool LoadStarted { get; internal set; }
}

public sealed class GpuThermalGuard
{
    public string PciAddress { get; }
    public double Limit { get; }
    public GpuThermalGuard(string pciAddress, double limit)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(pciAddress, @"\A[0-9a-f]{4}:[0-9a-f]{2}:[0-1][0-9a-f]\.[0-7]\z"))
            throw new ArgumentException("Endereço PCI inválido.", nameof(pciAddress));
        if (!double.IsFinite(limit) || limit is < 60 or > 90) throw new ArgumentOutOfRangeException(nameof(limit));
        PciAddress = pciAddress; Limit = limit;
    }

    public void Check(GpuTemperature reading)
    {
        if (reading.PciAddress != PciAddress) throw new GpuThermalException("Sensor pertence a outra GPU.");
        var age = Stopwatch.GetElapsedTime(reading.StartedTimestamp);
        if (reading.StartedTimestamp <= 0 || age < TimeSpan.Zero || age > TimeSpan.FromSeconds(2) ||
            reading.Error != null || reading.Celsius is not double value || !double.IsFinite(value) || value is < 0 or > 150)
            throw new GpuThermalException("Carga GPU recusada/interrompida: temperatura recente indisponível.");
        if (value >= Limit) throw new GpuThermalException($"Carga GPU interrompida: {value:N0} °C atingiu o limite de {Limit:N0} °C.");
    }
}
