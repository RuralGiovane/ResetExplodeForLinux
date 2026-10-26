using System.Globalization;
using ResetExplode.Hardware;

internal static class KernelLogClassificationChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    public static void Run()
    {
        (string Message, KernelLogContext Expected)[] examples =
        [
            ("mce: [Hardware Error]: Machine check events logged", KernelLogContext.CpuMemory),
            ("EDAC MC0: 1 CE on DIMM_A1", KernelLogContext.CpuMemory),
            ("EDAC MC12: UE row 0 channel 1", KernelLogContext.CpuMemory),
            ("NVRM: Xid (PCI:0000:01:00): 79, GPU has fallen off the bus", KernelLogContext.Graphics),
            ("amdgpu 0000:03:00.0: amdgpu: ring gfx timeout", KernelLogContext.Graphics),
            ("i915 0000:00:02.0: [drm] GPU HANG", KernelLogContext.Graphics),
            ("xe 0000:00:02.0: [drm] GT0: reset", KernelLogContext.Graphics),
            ("nouveau 0000:01:00.0: fifo: fault", KernelLogContext.Graphics),
            ("radeon 0000:01:00.0: ring stalled", KernelLogContext.Graphics),
            ("amdgpu: failed to load firmware", KernelLogContext.Graphics),
            ("[drm:amdgpu_job_timedout [amdgpu]] *ERROR* ring timeout", KernelLogContext.Graphics),
            ("pcieport 0000:00:1c.0: AER: Corrected error received", KernelLogContext.Pcie),
            ("0000:50:00.0: PCIe Bus Error: severity=Uncorrected (Fatal)", KernelLogContext.Pcie),
            ("nvme 0000:04:00.0: AER: can't recover", KernelLogContext.Pcie),
            ("amdgpu 0000:03:00.0: PCIe Bus Error: severity=Corrected", KernelLogContext.Graphics | KernelLogContext.Pcie),
            ("mce: record\nEDAC MC0: CE\nNVRM: Xid\npcieport 0000:00:1c.0: AER: error", KernelLogContext.CpuMemory | KernelLogContext.Graphics | KernelLogContext.Pcie),
            ("  I915 0000:00:02.0: warning", KernelLogContext.Graphics),
            // Contexto não implica erro: mantemos origem/associação sem diagnosticar defeito.
            ("mce: Machine check reporting enabled", KernelLogContext.CpuMemory),
            ("pcieport 0000:00:1c.0: AER disabled", KernelLogContext.Pcie),
            ("NVRM: loading driver", KernelLogContext.Graphics)
        ];
        foreach (var example in examples)
        {
            var result = KernelLogClassifier.Classify(example.Message);
            Check(result.Context == example.Expected, $"Contexto errado: {example.Message}");
            Check(!string.IsNullOrWhiteSpace(result.Evidence), "Faltou explicar a associação.");
        }
        foreach (string message in new[] { "", "GPU timeout", "CPU: 0 PID: 123", "Out of memory: Killed process",
            "usb: device mentions amdgpu and AER: timeout", "process NVRM: Xid example", "EDAC DEVICE0: error",
            "EDAC MC: driver loaded", "i915_fake: error", "xen: warning", "[Hardware Error]: unknown source",
            "nvme 0000:04:00.0: I/O timeout", "ACPI BIOS Error: failure", "text PCIe Bus Error: warning" })
        {
            var result = KernelLogClassifier.Classify(message);
            Check(result.Context == KernelLogContext.Unknown && result.Label == "Não classificado", $"Menção genérica classificada: {message}");
        }
        Check(KernelLogClassifier.Classify(new string('x', 8192) + "\nNVRM: Xid").Context == KernelLogContext.Unknown,
            "Classificou trecho que o usuário não pode ver.");
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Check(KernelLogClassifier.Classify("I915: warning").Context == KernelLogContext.Graphics, "Classificação depende da cultura.");
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
        string original = "amdgpu 0000:03:00.0: PCIe Bus Error: severity=Corrected";
        var entry = new KernelLogEntry(DateTimeOffset.UnixEpoch, 4, original);
        Check(entry.Message == original && entry.Description.EndsWith(original) && entry.Description.Contains("prioridade 4"), "Mensagem/prioridade alterada.");
        Check(entry.Description.Contains("Contexto indicativo: GPU/gráficos / PCIe"), "Descrição não explica múltiplos contextos.");
        var summary = KernelLogClassifier.Summarize([entry, new(DateTimeOffset.UnixEpoch, 3, "unrecognized")]);
        Check(summary.Contains("nos 2 registros") && summary.Contains("GPU/gráficos 1") && summary.Contains("PCIe 1")
            && summary.Contains("Não classificados 1") && summary.Contains("não de falhas"), "Resumo confunde contextos, registros e falhas.");
        Check(KernelLogClassifier.Summarize([]) == "Sem registros disponíveis para classificar.", "Ausência virou contagem zero de falhas.");
        Console.WriteLine("OK: classificação contextual CPU/memória, gráficos e PCIe, múltiplos contextos, negativos, cultura e preservação da mensagem.");
    }
}
