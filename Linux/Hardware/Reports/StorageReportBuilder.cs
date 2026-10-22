using System.Net;
using System.Text;
using System.Text.Json;

namespace ResetExplode.Hardware;

public sealed record StorageReport(DateTimeOffset GeneratedAt, string AppVersion,
    StorageInventory? Inventory, MountInventory? Mounts, DateTimeOffset? InventoryCollectedAt,
    SmartReport? Smart, DateTimeOffset? SmartCollectedAt)
{
    public int SchemaVersion => 1;
    public string ReportType => "linux-storage";
}

/// <summary>Exporta somente os dados já coletados, sem nova consulta ao hardware.</summary>
public static class StorageReportBuilder
{
    public static StorageReport Capture(string version, StorageInventory? inventory, MountInventory? mounts,
        DateTimeOffset? inventoryDate, SmartReport? smart, DateTimeOffset? smartDate) => new(
        DateTimeOffset.UtcNow, version,
        inventory == null ? null : inventory with { Devices = inventory.Devices.ToArray(), Notices = inventory.Notices.ToArray() },
        mounts == null ? null : mounts with { Mounts = mounts.Mounts.ToArray(), Notices = mounts.Notices.ToArray() },
        inventory == null ? null : inventoryDate,
        smart == null ? null : smart with { Metrics = smart.Metrics.ToArray(), Notices = smart.Notices.ToArray() },
        smart == null ? null : smartDate);

    public static string Json(StorageReport report) => JsonSerializer.Serialize(report, new JsonSerializerOptions
    { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "Indisponível");
    private static string Date(DateTimeOffset? value) => value?.ToString("O") ?? "Não coletado";

    public static string Html(StorageReport report)
    {
        var html = new StringBuilder("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>RESET EXPLODE · Armazenamento</title><style>body{background:#10131a;color:#e5e7eb;font:16px system-ui;max-width:1000px;margin:32px auto;padding:0 20px}article{background:#1b202b;padding:20px;margin:16px 0;border-radius:16px}p,td{overflow-wrap:anywhere}td,th{text-align:left;padding:8px;border-bottom:1px solid #444}table{width:100%;border-collapse:collapse}</style></head><body><h1>RESET EXPLODE · Armazenamento</h1>");
        html.Append($"<p>Linux · versão {E(report.AppVersion)} · exportado em {E(Date(report.GeneratedAt))}</p>");
        html.Append("<p>Dados da sessão, sem nova consulta durante a exportação. Inventário e SMART podem ter sido coletados em momentos diferentes. Capacidades compartilhadas não são somadas. Caminhos de montagem e origens estão incluídos.</p>");
        html.Append($"<h2>Dispositivos e partições</h2><p>Coleta: {E(Date(report.InventoryCollectedAt))}</p>");
        if (report.Inventory == null) html.Append("<p>Inventário não disponível.</p>");
        else
        {
            if (report.Inventory.Devices.Count == 0) html.Append("<p>Nenhum dispositivo registrado.</p>");
            foreach (var device in report.Inventory.Devices)
                html.Append($"<article><h3>{E(device.Title)}</h3><p>{E(device.Capacity)}</p><p>{E(device.Details)}</p></article>");
            foreach (var notice in report.Inventory.Notices) html.Append($"<p>{E(notice)}</p>");
        }
        html.Append("<h2>Sistemas de arquivos montados</h2>");
        if (report.Mounts == null) html.Append("<p>Montagens não disponíveis.</p>");
        else
        {
            if (report.Mounts.Mounts.Count == 0) html.Append("<p>Nenhuma montagem registrada.</p>");
            foreach (var mount in report.Mounts.Mounts)
            {
                html.Append($"<article><h3>{E(mount.MountPoint)}</h3><p>{E(mount.Details)}</p><p>{E(mount.Capacity)}</p>");
                if (mount.Notice != null) html.Append($"<p>{E(mount.Notice)}</p>");
                html.Append("</article>");
            }
            foreach (var notice in report.Mounts.Notices) html.Append($"<p>{E(notice)}</p>");
        }
        html.Append($"<h2>Última consulta SMART</h2><p>Coleta: {E(Date(report.SmartCollectedAt))}</p>");
        if (report.Smart is not { } smart) html.Append("<p>Nenhuma consulta SMART disponível.</p>");
        else
        {
            html.Append($"<article><h3>{E(smart.Device)} · {E(smart.Status)}</h3><table><tr><th>Métrica</th><th>Valor reportado</th></tr>");
            foreach (var metric in smart.Metrics) html.Append($"<tr><td>{E(metric.Name)}</td><td>{E(metric.Value)}</td></tr>");
            html.Append("</table>");
            foreach (var notice in smart.Notices) html.Append($"<p>{E(notice)}</p>");
            html.Append("</article>");
        }
        html.Append("<p>SMART refere-se apenas ao dispositivo identificado na consulta, sem associação automática ao inventário atual. Uma avaliação aprovada não garante ausência de falhas.</p></body></html>");
        return html.ToString();
    }
}
