using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResetExplode;

public sealed record ExportedBenchmark(BenchmarkHistoryEntry Result, bool SavedLocally);
public sealed record BenchmarkReport(DateTimeOffset GeneratedAt, string AppVersion, IReadOnlyList<ExportedBenchmark> Benchmarks)
{
    public int SchemaVersion => 1;
    public string Platform => "Linux";
}

/// <summary>Adapta a exportação HTML/JSON da base Windows ao histórico estruturado Linux.</summary>
public static class BenchmarkReportBuilder
{
    public static BenchmarkReport Capture(IEnumerable<BenchmarkHistoryEntry> saved,
        IEnumerable<BenchmarkHistoryEntry> pending, string version)
    {
        var unsaved = pending.ToList();
        var ids = unsaved.Select(e => e.Id).ToHashSet();
        var rows = unsaved.Concat(saved).DistinctBy(e => e.Id).OrderByDescending(e => e.Date)
            .Take(LocalPersistence.MaximumEntries).Select(e => new ExportedBenchmark(e with
            {
                Metrics = e.Outcome == BenchmarkOutcome.Completed ? new(e.Metrics) : new(),
                Score = e.Outcome == BenchmarkOutcome.Completed ? e.Score : null,
                Context = new(e.Context)
            }, !ids.Contains(e.Id))).ToArray();
        return new(DateTimeOffset.UtcNow, version, rows);
    }

    public static string Json(BenchmarkReport report) => JsonSerializer.Serialize(report, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    });

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    private static string Number(double value) => value.ToString("G", CultureInfo.InvariantCulture);

    public static string Html(BenchmarkReport report)
    {
        var html = new StringBuilder("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>RESET EXPLODE · Benchmarks</title><style>body{background:#10131a;color:#e5e7eb;font:16px system-ui;margin:32px auto;max-width:1000px;padding:0 20px}article{background:#1b202b;padding:20px;margin:20px 0;border-radius:16px}td,th{text-align:left;padding:8px;border-bottom:1px solid #444;overflow-wrap:anywhere}table{width:100%;border-collapse:collapse}p{overflow-wrap:anywhere}.muted{color:#b3baca}</style></head><body><h1>RESET EXPLODE · Benchmarks</h1>");
        html.Append($"<p>Linux · versão {E(report.AppVersion)} · gerado em {E(report.GeneratedAt.ToString("O"))}</p>");
        html.Append("<p class=\"muted\">Histórico disponível na sessão, limitado a 200 execuções. Inclui contexto do sistema. A comparação com Windows ainda não foi validada. NÃO SALVO indica pendência no histórico local, não neste relatório.</p>");
        if (report.Benchmarks.Count == 0) html.Append("<p>Nenhuma execução registrada.</p>");
        foreach (var row in report.Benchmarks)
        {
            var e = row.Result;
            string state = e.Outcome switch
            {
                BenchmarkOutcome.Completed => "CONCLUÍDO", BenchmarkOutcome.NotStarted => "NÃO INICIADO",
                BenchmarkOutcome.ThermalStopped => "INTERROMPIDO", BenchmarkOutcome.Cancelled => "CANCELADO", _ => "ERRO"
            };
            html.Append($"<article><h2>{E(e.Name)}</h2><p>{E(e.Date.ToString("O"))} · {state}{(row.SavedLocally ? "" : " · NÃO SALVO")}</p><p>{E(e.Summary)}</p>");
            html.Append($"<p>Monitoramento térmico: {(e.ThermalEnabled ? "ativado" : "desativado")} · limite {E(Number(e.ThermalLimit))} °C</p>");
            if (e.Outcome == BenchmarkOutcome.Completed)
            {
                if (e.Score is double score) html.Append($"<p>Pontuação: {E(Number(score))}</p>");
                html.Append("<table><tr><th>Métrica</th><th>Valor</th></tr>");
                foreach (var metric in e.Metrics) html.Append($"<tr><td>{E(metric.Key)}</td><td>{E(Number(metric.Value))}</td></tr>");
                html.Append("</table>");
            }
            html.Append("<h3>Contexto</h3><table>");
            foreach (var item in e.Context) html.Append($"<tr><td>{E(item.Key)}</td><td>{E(item.Value)}</td></tr>");
            html.Append("</table></article>");
        }
        html.Append("<footer>Gerado pelo RESET EXPLODE. Resultados incompletos não recebem pontuação.</footer></body></html>");
        return html.ToString();
    }
}
