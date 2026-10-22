using System.Diagnostics;

namespace ResetExplode;

public enum ThermalStopReason { TemperatureUnavailable, StaleReading, LimitReached }

/// <summary>Timestamp monotônico do início da coleta, não do momento em que a UI recebeu o dado.</summary>
public sealed record CpuTemperatureReading(double? Celsius, long Timestamp);

public sealed record ThermalStop(ThermalStopReason Reason, double? Temperature, double Limit)
{
    public string Message => Reason switch
    {
        ThermalStopReason.LimitReached => $"CPU em {Temperature:N1} °C; limite configurado de {Limit:N0} °C atingido.",
        ThermalStopReason.StaleReading => "A leitura térmica da CPU ficou desatualizada (limite de 5 s).",
        _ => "Temperatura da CPU indisponível; não foi possível manter o monitoramento térmico."
    };
}

public sealed class ThermalStopException(ThermalStop stop) : OperationCanceledException(stop.Message)
{
    public ThermalStop Stop { get; } = stop;
}

public sealed class CpuThermalPolicy
{
    public const double MinimumLimit = 60, MaximumLimit = 100, DefaultLimit = 90;
    public static readonly TimeSpan MaximumAge = TimeSpan.FromSeconds(5);
    public double Limit { get; }

    public CpuThermalPolicy(double limit)
    {
        if (!double.IsFinite(limit) || limit < MinimumLimit || limit > MaximumLimit)
            throw new ArgumentOutOfRangeException(nameof(limit));
        Limit = limit;
    }

    public ThermalStop? Evaluate(double? temperature, TimeSpan age)
    {
        if (temperature is not double value || !double.IsFinite(value))
            return new(ThermalStopReason.TemperatureUnavailable, null, Limit);
        if (age < TimeSpan.Zero || age >= MaximumAge)
            return new(ThermalStopReason.StaleReading, value, Limit);
        return value >= Limit ? new(ThermalStopReason.LimitReached, value, Limit) : null;
    }

    public ThermalStop? Evaluate(CpuTemperatureReading? reading) => reading == null
        ? Evaluate(null, TimeSpan.Zero)
        : Evaluate(reading.Celsius, Stopwatch.GetElapsedTime(reading.Timestamp));
}

/// <summary>Watchdog independente da UI; não consulta hardware, apenas o último snapshot publicado.</summary>
internal sealed class CpuThermalMonitor : IDisposable
{
    private readonly object _gate = new();
    private readonly CpuThermalPolicy _policy;
    private readonly Func<CpuTemperatureReading?> _read;
    private readonly CancellationTokenSource _cancellation;
    private readonly ManualResetEventSlim _shutdown = new();
    private readonly Thread _watchdog;
    private bool _disposed;
    private ThermalStop? _stop;

    public CpuThermalMonitor(CpuThermalPolicy policy, Func<CpuTemperatureReading?> read, CancellationToken cancellation)
    {
        _policy = policy;
        _read = read;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        Check();
        // O pool de threads é usado pelos benchmarks; não depender dele para detectar interrupções.
        _watchdog = new Thread(() =>
        {
            while (!_shutdown.Wait(TimeSpan.FromMilliseconds(250))) Check();
        }) { IsBackground = true, Name = "RESET Thermal" };
        _watchdog.Start();
    }

    public CancellationToken Token => _cancellation.Token;
    public ThermalStop? Stop { get { lock (_gate) return _stop; } }

    public void Check()
    {
        lock (_gate)
        {
            if (_disposed || _cancellation.IsCancellationRequested) return;
            try { _stop = _policy.Evaluate(_read()); }
            catch { _stop = new(ThermalStopReason.TemperatureUnavailable, null, _policy.Limit); }
            if (_stop != null)
            {
                _cancellation.Cancel();
                _shutdown.Set();
            }
        }
    }

    public void ThrowIfStopped()
    {
        if (Stop is { } stop) throw new ThermalStopException(stop);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _shutdown.Set();
        }
        _watchdog.Join();
        _shutdown.Dispose();
        _cancellation.Dispose();
    }
}
