using System.Globalization;
using System.Text.Json;
using ResetExplode.Hardware;

internal static class StabilityReportChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    public static void Run()
    {
        var date = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        const string boot = "00112233445566778899aabbccddeeff";
        var controllers = new List<EdacController> { new("mc0", "<script>controller</script>", 0, 0, 100) };
        var laterControllers = new List<EdacController> { new("mc0", "<script>controller</script>", ulong.MaxValue, 1, 110) };
        var messages = new List<KernelLogEntry> { new(date.AddSeconds(5), 3, "NVRM: <script>alert('x')</script> & text")
            { BootId = boot, MonotonicMicroseconds = 105_000_000 } };
        var edac = new EdacSnapshot(date, controllers);
        var later = new EdacSnapshot(date.AddSeconds(10), laterControllers);
        var journal = new KernelLogSnapshot(date.AddSeconds(11), messages, "Amostra <incompleta>");
        var report = StabilityReportBuilder.Capture("<version>", edac, "EDAC <ok>", journal, "Logs & disponíveis",
            new(boot, 100_000_000, 100_001_000, edac), new(boot, 110_000_000, 110_001_000, later), journal, "Comparação concluída.");
        controllers.Clear(); laterControllers.Clear(); messages.Clear();
        Check(report.Edac!.Controllers.Count == 1 && report.KernelLogs!.Entries.Count == 1 && report.Baseline!.Edac.Controllers.Count == 1
            && report.Current!.Edac.Controllers.Count == 1 && report.SessionLogs!.Entries.Count == 1, "Snapshot compartilhou coleções.");
        Check(report.Comparison!.Controllers[0].Corrected == ulong.MaxValue && report.Comparison.Logs.Count == 1, "Comparação perdeu precisão/período.");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            string html = StabilityReportBuilder.Html(report);
            Check(!html.Contains("<script>") && html.Contains("&lt;script&gt;") && html.Contains("&lt;version&gt;") && html.Contains("&amp;"), "HTML não escapado.");
            Check(html.Contains(date.ToString("O")) && html.Contains(journal.Timestamp.ToString("O")) && html.Contains("18446744073709551615"), "HTML perdeu datas/precisão.");
            Check(html.Contains("Amostra &lt;incompleta&gt;") && System.Net.WebUtility.HtmlDecode(html).Contains("não certifica estabilidade"), "Limites ausentes.");
            using var json = JsonDocument.Parse(StabilityReportBuilder.Json(report));
            var root = json.RootElement;
            Check(root.GetProperty("schemaVersion").GetInt32() == 1 && root.GetProperty("reportType").GetString() == "linux-stability", "Esquema incorreto.");
            Check(root.GetProperty("comparison").GetProperty("controllers")[0].GetProperty("corrected").GetUInt64() == ulong.MaxValue, "JSON perdeu uint64.");
            Check(root.GetProperty("edac").GetProperty("timestamp").GetDateTimeOffset() == date &&
                root.GetProperty("sessionLogs").GetProperty("timestamp").GetDateTimeOffset() == journal.Timestamp, "Datas independentes perdidas.");
            var entry = root.GetProperty("kernelLogs").GetProperty("entries")[0];
            Check(entry.GetProperty("classification").GetProperty("context").GetString() == "Graphics" &&
                entry.GetProperty("monotonicMicroseconds").GetUInt64() == 105_000_000 && entry.GetProperty("message").GetString()!.Contains("<script>"), "JSON alterou mensagem/contexto/metadados.");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        var missing = StabilityReportBuilder.Capture("1", null, "Consulta ainda não realizada.", null, "Consulta cancelada.", null, null, null, "Nenhuma referência criada.");
        Check(System.Net.WebUtility.HtmlDecode(StabilityReportBuilder.Html(missing)).Contains("Nenhuma comparação disponível") && StabilityReportBuilder.Html(missing).Contains("Consulta cancelada."), "Estados ausentes/cancelados perdidos.");
        using var missingJson = JsonDocument.Parse(StabilityReportBuilder.Json(missing));
        foreach (string field in new[] { "edac", "kernelLogs", "baseline", "current", "sessionLogs", "comparison" })
            Check(missingJson.RootElement.GetProperty(field).ValueKind == JsonValueKind.Null, "Ausência virou coleta vazia.");
        var pending = StabilityReportBuilder.Capture("1", null, "Lendo…", null, "Falha <leitura>", report.Baseline, null, null, "Comparando…");
        Check(pending.Baseline != null && pending.Comparison == null && StabilityReportBuilder.Html(pending).Contains("Falha &lt;leitura&gt;"), "Resultado antigo inventado durante consulta/falha.");
        var empty = StabilityReportBuilder.Capture("1", new(date, []), "Indisponível", new(date, [], "Sem acesso ao journal"), "Concluído", null, null, null, "Sem sessão");
        Check(empty.Edac != null && empty.KernelLogs != null && StabilityReportBuilder.Html(empty).Contains("Sem acesso ao journal"), "Coleta vazia/indisponível perdida.");
        var invalid = StabilityReportBuilder.Capture("1", null, "", null, "", report.Baseline,
            report.Current! with { BootId = "other" }, report.SessionLogs, "Concluído");
        Check(invalid.Comparison!.Status.Contains("indisponível") && invalid.Comparison.Controllers.Count == 0, "Reboot exportou diferenças inválidas.");
        Console.WriteLine("OK: relatório de estabilidade HTML/JSON, snapshot, escaping, uint64, datas, contexto e estados incompletos.");
    }
}
