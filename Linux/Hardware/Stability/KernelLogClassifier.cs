using System.Text.RegularExpressions;

namespace ResetExplode.Hardware;

[Flags]
public enum KernelLogContext { Unknown = 0, CpuMemory = 1, Graphics = 2, Pcie = 4 }

public sealed record KernelLogClassification(KernelLogContext Context, string Evidence)
{
    public string Label => Context == KernelLogContext.Unknown ? "Não classificado" : string.Join(" / ",
        new[] { (KernelLogContext.CpuMemory, "CPU/memória"), (KernelLogContext.Graphics, "GPU/gráficos"),
            (KernelLogContext.Pcie, "PCIe") }.Where(item => Context.HasFlag(item.Item1)).Select(item => item.Item2));
}

/// <summary>Associações textuais de contexto, sem diagnóstico, severidade inferida ou contagem de falhas.</summary>
public static class KernelLogClassifier
{
    public const string Methodology = "kernel-context-v1";
    private const string Address = @"(?:[0-9a-f]{4}:)?[0-9a-f]{2}:[0-9a-f]{2}\.[0-7]";
    private sealed record Rule(KernelLogContext Context, Regex Pattern, string Evidence);
    private static Rule Match(KernelLogContext context, string pattern, string evidence) => new(context,
        new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline |
            RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100)), evidence);
    // Prefixos de linhas, não menções soltas a nomes de drivers no meio de uma mensagem.
    // EDAC MC é específico de controladores de memória; EDAC genérico pode descrever outros dispositivos.
    private static readonly Rule[] Rules =
    [
        Match(KernelLogContext.CpuMemory, @"^[ \t]*mce:", "Prefixo mce (machine check)"),
        Match(KernelLogContext.CpuMemory, @"^[ \t]*EDAC[ \t]+MC[0-9]+\b", "Prefixo EDAC MC (controlador de memória)"),
        Match(KernelLogContext.Graphics, @"^[ \t]*NVRM:", "Prefixo NVRM (driver NVIDIA)"),
        Match(KernelLogContext.Graphics, @"^[ \t]*(?:amdgpu|radeon|i915|xe|nouveau)(?:[ \t]+" + Address + @")?:",
            "Prefixo de driver gráfico reconhecido"),
        Match(KernelLogContext.Graphics, @"^[ \t]*(?:amdgpu|radeon|i915|xe|nouveau)[ \t]+" + Address + @"[ \t]",
            "Driver gráfico e endereço PCI no início da linha"),
        Match(KernelLogContext.Graphics, @"^[ \t]*\[drm(?::[^\]\r\n]+)?\]", "Prefixo DRM (subsistema gráfico)"),
        Match(KernelLogContext.Pcie, @"^[ \t]*pcieport[ \t]+" + Address + @":", "Prefixo pcieport (porta PCIe)"),
        Match(KernelLogContext.Pcie, @"^[ \t]*(?:(?:[a-z0-9_-]+[ \t]+)?" + Address + @":[ \t]*)?(?:PCIe Bus Error:|AER:)",
            "Marcador PCIe Bus Error ou AER no início da mensagem do dispositivo")
    ];

    public static KernelLogClassification Classify(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var context = KernelLogContext.Unknown;
        var evidence = new List<string>();
        // Mesma janela máxima exibida pelo leitor; nunca atribuir contexto por um trecho oculto.
        string visible = message.Length > 8192 ? message[..8192] : message;
        try
        {
            foreach (var rule in Rules)
            {
                if (!rule.Pattern.IsMatch(visible)) continue;
                context |= rule.Context;
                evidence.Add(rule.Evidence);
            }
        }
        catch (RegexMatchTimeoutException)
        { return new(KernelLogContext.Unknown, "Análise textual excedeu o prazo; consulte a mensagem."); }
        return new(context, evidence.Count == 0 ? "Nenhum marcador reconhecido nesta mensagem." : string.Join("; ", evidence));
    }

    public static string Summarize(IReadOnlyList<KernelLogEntry> entries)
    {
        if (entries.Count == 0) return "Sem registros disponíveis para classificar.";
        int cpu = 0, graphics = 0, pcie = 0, unknown = 0;
        foreach (var entry in entries)
        {
            var context = entry.Classification.Context;
            if (context == KernelLogContext.Unknown) unknown++;
            if (context.HasFlag(KernelLogContext.CpuMemory)) cpu++;
            if (context.HasFlag(KernelLogContext.Graphics)) graphics++;
            if (context.HasFlag(KernelLogContext.Pcie)) pcie++;
        }
        return $"Contextos indicados nos {entries.Count} registros retornados: CPU/memória {cpu} · GPU/gráficos {graphics} · PCIe {pcie} · Não classificados {unknown}. " +
            "Contagens de mensagens, não de falhas. Uma mensagem pode ter mais de um contexto; registros sem classificação continuam abaixo.";
    }
}
