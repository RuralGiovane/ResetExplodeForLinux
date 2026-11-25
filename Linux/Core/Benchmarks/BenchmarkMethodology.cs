using System.Globalization;

namespace ResetExplode;

public static class BenchmarkMethodology
{
    public static Dictionary<string, string> Parameters(string test)
    {
        var result = new Dictionary<string, string>
        {
            ["methodology"] = test == "ram-bandwidth" ? "linux-ram-bandwidth-v2" : $"linux-{test}-v1",
            ["logicalProcessors"] = Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture),
            ["telemetry"] = "active; may affect timing",
            ["crossPlatformComparison"] = "not validated"
        };
        switch (test)
        {
            case "gpu-stress":
                result["measurement"] = "Vulkan 1.1 offscreen torus; completed frames; no score or stability verdict";
                result["resolution"] = "640x360";
                result["shells"] = "32";
                result["aluIterations"] = "32";
                result["duration"] = "render loop only; excludes initialization/readback; waits for submitted frame";
                result["thermal"] = "mandatory selected PCI GPU sensor; NVIDIA NVML or device-local hwmon; no CPU fallback";
                break;
            case "cpu-stress":
            case "ram-stress":
            case "cpu-ram-stress":
                result["measurement"] = test switch
                {
                    "cpu-stress" => "bounded CPU FMA/sqrt/sin/integer load; no performance score or stability verdict",
                    "ram-stress" => "bounded RAM pattern write/verify; no performance score or stability verdict",
                    _ => "bounded CPU FMA/sqrt/sin/integer load and RAM pattern write/verify; no performance score or stability verdict"
                };
                result["memory"] = "explicit MiB, maximum 1024; at most one quarter of observed MemAvailable/runtime budget; availability may change";
                result["duration"] = "includes allocation; incomplete RAM passes not counted; cooperative deadline";
                result["thermal"] = "mandatory CPU temperature watchdog; does not monitor DIMM temperature";
                result["hardwareErrors"] = "EDAC and journal are separate manual observations, not automatically attributed to this workload";
                break;
            case "disk-direct":
            case "disk-loading":
                result["io"] = "O_DIRECT; alignment via STATX_DIOALIGN or Btrfs sector ioctl; 4096-byte aligned buffers/offsets; writes O_DSYNC";
                result["btrfs"] = "temporary files only: verified NoCoW/NoCompress; not representative of default CoW/checksummed files";
                result["fileBytes"] = "536870912";
                result["sequentialBlockBytes"] = "1048576";
                result["units"] = "MiB/s (1048576 bytes); IOPS; mean operation duration in microseconds";
                result["cacheLimits"] = "device/controller and metadata caches may contribute; no global cache changes";
                result["data"] = "seeded 1 MiB buffer reused; compression/deduplication may affect results";
                result["repetitions"] = "one run; no aggregation or cross-platform score";
                result["parallelism"] = "up to 8 synchronous workers, not guaranteed device queue depth";
                if (test == "disk-direct")
                {
                    result["randomBlockBytes"] = "4096";
                    result["randomPhaseSeconds"] = "4";
                    result["measurement"] = "sequential write then read; random read/write single worker; random read up to 8 workers; allocation included in sequential write";
                }
                else
                {
                    result["assetCount"] = "1000";
                    result["assetBytes"] = "65536";
                    result["warmup"] = "one unmeasured asset read pass; preparation writes excluded";
                    result["measurement"] = "pack read plus measured assets open/read/close; includes statx and worker scheduling; not real game load time";
                }
                break;
            case "cpu-render":
                result["warmup"] = "none";
                result["measurement"] = "one complete pass; samples/seconds/10000 points";
                break;
            case "cpu-simulation":
                result["warmupSeconds"] = "2";
                result["low1Definition"] = "1000 / sorted frame time at floor(count * 0.99), clamped to last index";
                result["measurement"] = "synthetic CPU simulation; not game rendering FPS";
                break;
            case "ram-latency":
                result["bufferBytes"] = "268435456";
                result["orderBytes"] = "16777216";
                result["seed"] = "42";
                result["strideBytes"] = "64";
                result["warmupSteps"] = "200000";
                result["measuredSteps"] = "20000000";
                result["measurement"] = "dependent reads; allocation and permutation excluded; cache/TLB may contribute";
                break;
            case "ram-bandwidth":
                result["bufferBytesEach"] = "1073741824";
                result["buffers"] = "2";
                result["repetitions"] = "3";
                result["aggregation"] = "independent maximum for read and copy";
                result["warmup"] = "source initialized outside timing; no separate warmup; destination first touch may be timed";
                result["units"] = "decimal GB/s; read=bytes/seconds; copy=2*bytes/seconds";
                result["measurement"] = "all elements; 65536-element cancellation chunks; logical traffic, not physical DRAM bandwidth";
                break;
        }
        return result;
    }
}
