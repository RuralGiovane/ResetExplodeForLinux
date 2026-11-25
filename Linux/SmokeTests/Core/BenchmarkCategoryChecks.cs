using ResetExplode;

internal static class BenchmarkCategoryChecks
{
    public static void Run()
    {
        foreach (var (test, component) in new[] {
            ("cpu-render", "cpu"), ("cpu-simulation", "cpu"), ("cpu-stress", "cpu"),
            ("ram-latency", "ram"), ("ram-bandwidth", "ram"), ("ram-stress", "ram"),
            ("gpu-stress", "gpu"), ("disk-direct", "disk"), ("disk-loading", "disk") })
            foreach (var category in new[] { "cpu", "ram", "gpu", "disk" })
                if (BenchmarkCategory.Includes(test, category) != (component == category))
                    throw new Exception($"Histórico incorreto: {test} em {category}");
        if (!BenchmarkCategory.Includes("cpu-ram-stress", "cpu") || !BenchmarkCategory.Includes("cpu-ram-stress", "ram") ||
            BenchmarkCategory.Includes("unknown", "cpu")) throw new Exception("Histórico legado/ desconhecido incorreto.");
        foreach (var test in new[] { "cpu-stress", "ram-stress" })
            if (!BenchmarkMethodology.Parameters(test)["thermal"].Contains("mandatory"))
                throw new Exception("Metodologia de stress sem proteção obrigatória.");
        Console.WriteLine("OK: históricos por componente, stress legado e metodologia dos stresses separados.");
    }
}
