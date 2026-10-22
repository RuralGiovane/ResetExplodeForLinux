using System.Globalization;
using System.Text.Json;
using ResetExplode;

internal static class ReportChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            var completed = new BenchmarkHistoryEntry
            {
                Id = Guid.NewGuid(), Date = DateTimeOffset.UtcNow, Test = "cpu-render", Name = "<script>alert(1)</script>",
                Summary = "CPU & RAM", Outcome = BenchmarkOutcome.Completed, Score = 12.5,
                Metrics = new() { ["seconds"] = 1.25 }, Context = new() { ["cpu"] = "<CPU>" }
            };
            var incomplete = completed with { Id = Guid.NewGuid(), Outcome = BenchmarkOutcome.Cancelled };
            var report = BenchmarkReportBuilder.Capture([completed], [completed, incomplete], "0.16");
            Check(report.Benchmarks.Count == 2 && report.Benchmarks.All(e => !e.SavedLocally), "Exportação duplicou ou perdeu pendências.");
            completed.Context["cpu"] = "alterado depois";
            Check(report.Benchmarks.All(e => e.Result.Context["cpu"] == "<CPU>"), "Snapshot compartilha contexto mutável.");
            string html = BenchmarkReportBuilder.Html(report);
            Check(!html.Contains("<script>") && html.Contains("&lt;script&gt;") && html.Contains("CPU &amp; RAM"), "HTML não escapou texto externo.");
            Check(html.Contains("NÃO SALVO") && html.Contains("CANCELADO"), "HTML perdeu os estados das execuções.");
            using var json = JsonDocument.Parse(BenchmarkReportBuilder.Json(report));
            var rows = json.RootElement.GetProperty("benchmarks").EnumerateArray().ToArray();
            var good = rows.Single(e => e.GetProperty("result").GetProperty("outcome").GetString() == "completed").GetProperty("result");
            Check(good.GetProperty("score").GetDouble() == 12.5 && good.GetProperty("metrics").GetProperty("seconds").GetDouble() == 1.25, "JSON perdeu métricas numéricas em pt-BR.");
            var bad = rows.Single(e => e.GetProperty("result").GetProperty("outcome").GetString() == "cancelled").GetProperty("result");
            Check(bad.GetProperty("score").ValueKind == JsonValueKind.Null && !bad.GetProperty("metrics").EnumerateObject().Any(), "Exportação incluiu pontuação parcial.");
            Check(BenchmarkReportBuilder.Capture([completed], [], "0.16").Benchmarks.Single().SavedLocally, "Registro salvo foi marcado pendente.");
            var many = Enumerable.Range(0, 205).Select(i => completed with { Id = Guid.NewGuid(), Date = completed.Date.AddSeconds(i) });
            Check(BenchmarkReportBuilder.Capture(many, [], "0.16").Benchmarks.Count == 200, "Exportação excedeu retenção.");
            var empty = BenchmarkReportBuilder.Capture([], [], "0.16");
            Check(BenchmarkReportBuilder.Html(empty).Contains("Nenhuma execução registrada"), "Relatório vazio não foi tratado.");
            Console.WriteLine("OK: relatório HTML/JSON, escaping, snapshot, cultura, estados e retenção.");
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
}
