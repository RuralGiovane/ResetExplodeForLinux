namespace ResetExplode;

public static class BenchmarkCategory
{
    // Legacy combined stress remains visible in both component histories.
    public static bool Includes(string test, string component) => component switch
    {
        "cpu" => test is "cpu-render" or "cpu-simulation" or "cpu-stress" or "cpu-ram-stress",
        "ram" => test is "ram-latency" or "ram-bandwidth" or "ram-stress" or "cpu-ram-stress",
        "gpu" => test == "gpu-stress",
        "disk" => test is "disk-direct" or "disk-loading",
        _ => false
    };
}
