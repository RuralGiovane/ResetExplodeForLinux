using System.Runtime.InteropServices;
using System.Text;

namespace ResetExplode.Hardware;

/// <summary>NVML somente de leitura. Uso serial, incluindo Dispose, após encerrar o polling.</summary>
public sealed class LinuxNvml : IGpuSensorSource
{
    private IntPtr _library;
    private bool _attempted, _initialized, _disposed;
    private string _status = "NVIDIA: ainda não consultada.";
    private readonly Dictionary<string, Delegate> _exports = new(StringComparer.Ordinal);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NoArgs();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Count(out uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Device(uint index, out IntPtr handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Name(IntPtr device, StringBuilder name, uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int UIntValue(IntPtr device, out uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FieldValue(IntPtr device, uint field, out uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int MemoryValue(IntPtr device, out MemoryInfo value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int LoadValue(IntPtr device, out Utilization value);
    [StructLayout(LayoutKind.Sequential)] private struct MemoryInfo { public ulong Total, Free, Used; }
    [StructLayout(LayoutKind.Sequential)] private struct Utilization { public uint Gpu, Memory; }

    private T? Export<T>(string name) where T : Delegate
    {
        if (_exports.TryGetValue(name, out var cached)) return (T)cached;
        if (_library == IntPtr.Zero || !NativeLibrary.TryGetExport(_library, name, out var pointer)) return null;
        var result = Marshal.GetDelegateForFunctionPointer<T>(pointer);
        _exports[name] = result;
        return result;
    }

    private void Initialize()
    {
        if (_attempted) return;
        _attempted = true;
        if (!OperatingSystem.IsLinux()) { _status = "NVIDIA: coletor disponível apenas no Linux."; return; }
        try
        {
            if (!NativeLibrary.TryLoad("libnvidia-ml.so.1", out _library) &&
                !NativeLibrary.TryLoad("libnvidia-ml.so", out _library))
            { _status = "NVIDIA: biblioteca NVML não disponível (opcional)."; return; }
            var init = Export<NoArgs>("nvmlInit_v2");
            if (init == null || Export<NoArgs>("nvmlShutdown") == null ||
                Export<Count>("nvmlDeviceGetCount_v2") == null || Export<Device>("nvmlDeviceGetHandleByIndex_v2") == null)
            { _status = "NVIDIA: funções essenciais ausentes na biblioteca NVML."; Release(); return; }
            int result = init();
            if (result != 0) { _status = $"NVIDIA: falha ao iniciar NVML (código {result})."; Release(); return; }
            _initialized = true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        { _status = $"NVIDIA: não foi possível carregar NVML ({ex.GetType().Name})."; Release(); }
    }

    public GpuSnapshot Read(CancellationToken cancellation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellation.ThrowIfCancellationRequested();
        Initialize();
        if (!_initialized) return new([], _status);
        var rows = new List<SensorReading>();
        int countResult = Export<Count>("nvmlDeviceGetCount_v2")!(out uint count);
        if (countResult != 0) return new([], $"NVIDIA: falha ao enumerar GPUs (código {countResult}).");
        int failedDevices = 0;
        for (uint index = 0; index < count; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            int deviceResult = Export<Device>("nvmlDeviceGetHandleByIndex_v2")!(index, out var handle);
            if (deviceResult != 0) { failedDevices++; continue; }
            var name = new StringBuilder(128);
            string hardware = Export<Name>("nvmlDeviceGetName")?.Invoke(handle, name, (uint)name.Capacity) == 0
                ? name.ToString() : $"NVIDIA GPU {index}";
            hardware += $" · GPU {index}";
            void Add(string id, string label, SensorKind kind, double? value, string unit, int code) =>
                rows.Add(new($"nvml/{index}/{id}", hardware, label, kind, HardwareComponent.Gpu,
                    code == 0 ? value : null, unit, code == 0 ? null : code == -1 ? "Função indisponível neste driver." : $"NVML: código {code}."));

            void ReadUInt(string symbol, string id, string label, SensorKind kind, double divisor, string unit)
            {
                uint value = 0;
                int code = Export<UIntValue>(symbol)?.Invoke(handle, out value) ?? -1;
                Add(id, label, kind, value / divisor, unit, code);
            }
            void ReadField(string symbol, uint field, string id, string label, SensorKind kind, string unit)
            {
                uint value = 0;
                int code = Export<FieldValue>(symbol)?.Invoke(handle, field, out value) ?? -1;
                Add(id, label, kind, value, unit, code);
            }

            ReadField("nvmlDeviceGetTemperature", 0, "temperature", "Temperatura", SensorKind.Temperature, "°C");
            ReadField("nvmlDeviceGetClockInfo", 0, "graphicsClock", "Clock gráfico", SensorKind.Clock, "MHz");
            ReadField("nvmlDeviceGetClockInfo", 2, "memoryClock", "Clock da memória", SensorKind.Clock, "MHz");
            ReadUInt("nvmlDeviceGetPowerUsage", "power", "Consumo", SensorKind.Power, 1000, "W");
            ReadUInt("nvmlDeviceGetEnforcedPowerLimit", "powerLimit", "Limite de potência", SensorKind.Power, 1000, "W");
            Utilization utilization = default;
            int loadCode = Export<LoadValue>("nvmlDeviceGetUtilizationRates")?.Invoke(handle, out utilization) ?? -1;
            Add("load", "Uso da GPU", SensorKind.Load, utilization.Gpu, "%", loadCode);
            MemoryInfo memory = default;
            int memoryCode = Export<MemoryValue>("nvmlDeviceGetMemoryInfo")?.Invoke(handle, out memory) ?? -1;
            Add("memoryUsed", "VRAM usada", SensorKind.Memory, memory.Used / 1073741824.0, "GiB", memoryCode);
            Add("memoryTotal", "VRAM total", SensorKind.Memory, memory.Total / 1073741824.0, "GiB", memoryCode);
        }
        return new(rows, $"NVIDIA: {count} GPU(s); {failedDevices} dispositivo(s) inacessível(is). Métricas dependem do driver.");
    }

    private void Release()
    {
        if (_library == IntPtr.Zero) return;
        if (_initialized) Export<NoArgs>("nvmlShutdown")?.Invoke();
        _initialized = false;
        _exports.Clear();
        NativeLibrary.Free(_library);
        _library = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Release();
        _disposed = true;
    }
}
