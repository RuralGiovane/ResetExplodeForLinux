using System.Net;
using System.Text;
using System.Text.Json;

namespace ResetExplode.Hardware;

public sealed record PowerReport(DateTimeOffset GeneratedAt, string AppVersion, PowerInventory? Inventory, string? CollectionError)
{
    public int SchemaVersion => 1;
    public string ReportType => "linux-power";
}

public static class PowerReportBuilder
{
    public static PowerReport Capture(string version, PowerInventory? inventory, string? error = null) => new(
        DateTimeOffset.UtcNow, version, inventory == null ? null : inventory with
        {
            Supplies = inventory.Supplies.Select(s => s with { Metrics = s.Metrics.ToArray() }).ToArray(),
            Notices = inventory.Notices.ToArray()
        }, error);

    public static string Json(PowerReport report) => JsonSerializer.Serialize(report, new JsonSerializerOptions
    { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "Indisponível");

    public static string Html(PowerReport report)
    {
        var html = new StringBuilder("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>RESET EXPLODE · Alimentação</title><style>body{background:#10131a;color:#e5e7eb;font:16px system-ui;max-width:1000px;margin:32px auto;padding:0 20px}article{background:#1b202b;padding:20px;margin:16px 0;border-radius:16px}p,td{overflow-wrap:anywhere}td,th{text-align:left;padding:8px;border-bottom:1px solid #444}table{width:100%;border-collapse:collapse}</style></head><body><h1>RESET EXPLODE · Bateria e alimentação</h1>");
        html.Append($"<p>Linux · versão {E(report.AppVersion)} · exportado em {E(report.GeneratedAt.ToString("O"))}</p>");
        html.Append("<p>Dados da sessão, sem nova consulta ao exportar. Valores e unidades correspondem à apresentação da coleta. Energia (Wh) e carga elétrica (Ah) são grandezas diferentes. Leituras ausentes não indicam falha.</p>");
        if (report.CollectionError != null) html.Append($"<p>Falha de coleta: {E(report.CollectionError)}</p>");
        if (report.Inventory is not { } inventory) html.Append("<p>Nenhuma coleta de alimentação disponível.</p>");
        else
        {
            html.Append($"<p>Coletado em {E(inventory.Timestamp.ToString("O"))}</p>");
            if (inventory.Supplies.Count == 0) html.Append("<p>Nenhuma fonte/bateria registrada na coleta.</p>");
            foreach (var supply in inventory.Supplies)
            {
                html.Append($"<article><h2>{E(supply.Name)}</h2><p>{E(supply.Type)} · {E(supply.Scope)}</p><table><tr><th>Métrica</th><th>Valor reportado</th></tr>");
                foreach (var metric in supply.Metrics) html.Append($"<tr><td>{E(metric.Name)}</td><td>{E(metric.Value)}</td></tr>");
                html.Append("</table></article>");
            }
            foreach (var notice in inventory.Notices) html.Append($"<p>{E(notice)}</p>");
        }
        html.Append("<p>Estado online descreve cada fonte, não a alimentação global do computador. Este relatório não estima desgaste, autonomia ou saúde.</p></body></html>");
        return html.ToString();
    }
}
