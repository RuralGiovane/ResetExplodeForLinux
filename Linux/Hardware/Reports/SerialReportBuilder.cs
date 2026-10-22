using System.Net;
using System.Text;
using System.Text.Json;

namespace ResetExplode.Hardware;

public sealed record SerialReport(DateTimeOffset GeneratedAt, string AppVersion, SerialInventory? Inventory, string? CollectionError)
{
    public int SchemaVersion => 1;
    public string ReportType => "linux-serial";
}

public static class SerialReportBuilder
{
    public static SerialReport Capture(string version, SerialInventory? inventory, string? error = null) => new(
        DateTimeOffset.UtcNow, version, inventory == null ? null : inventory with
        {
            Ports = inventory.Ports.Select(p => p with { Aliases = p.Aliases.ToArray(), Usb = p.Usb is { } usb ? usb with { } : null }).ToArray(),
            Groups = inventory.Groups.Select(g => g with { }).ToArray(),
            Notices = inventory.Notices.ToArray()
        }, error);

    public static string Json(SerialReport report) => JsonSerializer.Serialize(report, new JsonSerializerOptions
    { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "Indisponível");

    public static string Html(SerialReport report)
    {
        var html = new StringBuilder("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>RESET EXPLODE · Maker</title><style>body{background:#10131a;color:#e5e7eb;font:16px system-ui;max-width:1000px;margin:32px auto;padding:0 20px}article{background:#1b202b;padding:20px;margin:16px 0;border-radius:16px}p,li{overflow-wrap:anywhere}</style></head><body><h1>RESET EXPLODE · Portas seriais</h1>");
        html.Append($"<p>Linux · versão {E(report.AppVersion)} · exportado em {E(report.GeneratedAt.ToString("O"))}</p>");
        html.Append("<p>Inventário da sessão, sem nova coleta ao exportar. Não comprova acesso, comunicação ou funcionamento. Caminhos by-id podem conter identificadores e números de série.</p>");
        if (report.CollectionError != null) html.Append($"<p>Falha de coleta: {E(report.CollectionError)}</p>");
        if (report.Inventory is not { } inventory) html.Append("<p>Nenhuma coleta serial disponível.</p>");
        else
        {
            html.Append($"<p>Coletado em {E(inventory.Timestamp.ToString("O"))}</p>");
            if (inventory.Ports.Count == 0) html.Append("<p>Nenhuma porta registrada na coleta.</p>");
            foreach (var port in inventory.Ports)
            {
                html.Append($"<article><h2>{E(port.Title)}</h2><p>{E(port.Details)}</p><p>{E(port.HardwareDescription)}</p>");
                if (port.Usb is { } usb)
                    html.Append($"<p>Origem USB: {E(usb.DevicePath)} · origem do driver: {E(usb.DriverDevicePath)}</p>");
                if (port.Aliases.Count == 0) html.Append("<p>Nenhum caminho by-id associado observado.</p>");
                else
                {
                    html.Append("<ul>");
                    foreach (string alias in port.Aliases) html.Append($"<li>{E(alias)}</li>");
                    html.Append("</ul>");
                }
                html.Append("</article>");
            }
            html.Append("<h2>Grupos da sessão</h2>");
            foreach (var group in inventory.Groups) html.Append($"<p>{E(group.Description)}</p>");
            foreach (string notice in inventory.Notices) html.Append($"<p>{E(notice)}</p>");
        }
        html.Append("</body></html>");
        return html.ToString();
    }
}
