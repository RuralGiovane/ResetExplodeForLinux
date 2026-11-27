namespace ResetExplode.Hardware;

/// <summary>Published reference specifications, not measurements of the installed board.</summary>
internal static class GpuReferenceCatalog
{
    internal sealed record Specification(string Model, string MemoryType, string Process);

    // Verified 2026-11-26. Only IDs with an unambiguous model and sourced fields belong here.
    // Identity: NVIDIA driver README. Specifications: Table 1 of NVIDIA's Turing article.
    // Do not expand this table by matching a family name or by choosing a first variant.
    internal const string IdentitySource = "https://download.nvidia.com/XFree86/Linux-x86_64/590.48.01/README/supportedchips.html";
    internal const string SpecificationSource = "https://developer.nvidia.com/blog/nvidia-turing-architecture-in-depth/";

    private static readonly IReadOnlyDictionary<uint, Specification> Nvidia =
        new Dictionary<uint, Specification>
        {
            [0x1b06] = new("GeForce GTX 1080 Ti", "GDDR5X", "16 nm"),
            [0x1e04] = new("GeForce RTX 2080 Ti", "GDDR6", "12 nm FFN"),
            [0x1e07] = new("GeForce RTX 2080 Ti", "GDDR6", "12 nm FFN")
        };

    internal static Specification? Find(uint? vendor, uint? device, string? reportedName)
    {
        if (vendor != 0x10de || device is not uint id || !Nvidia.TryGetValue(id, out var spec)) return null;
        // A reported conflicting name invalidates the reference. No substring/fuzzy fallback.
        if (!string.IsNullOrWhiteSpace(reportedName))
        {
            string name = reportedName.Trim();
            if (name.StartsWith("NVIDIA ", StringComparison.OrdinalIgnoreCase)) name = name[7..];
            if (!name.Equals(spec.Model, StringComparison.OrdinalIgnoreCase)) return null;
        }
        return spec;
    }
}
