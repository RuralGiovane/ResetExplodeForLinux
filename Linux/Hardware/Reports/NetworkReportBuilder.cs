using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResetExplode.Hardware;

public sealed record NetworkReport(DateTimeOffset GeneratedAt, string AppVersion, NetworkInventory? Inventory,
    string? CollectionError, NetworkLatencyResult? Latency, DateTimeOffset? LatencyCompletedAt, string LatencyStatus)
{
    public NetworkThroughputResult? Throughput { get; init; }
    public string ThroughputStatus { get; init; } = "Nenhum teste de transferência executado.";
    public string ThroughputMethodology => NetworkThroughputTest.Methodology;
    public NetworkDnsResult? DnsResolution { get; init; }
    public string DnsResolutionStatus { get; init; } = "Nenhuma resolução executada.";
    public string DnsMethodology => NetworkDnsTest.Methodology;
    public int SchemaVersion => 3;
    public string ReportType => "linux-network";
    public string Methodology => "10 tentativas ICMP; timeout de 2 s; pausa de 500 ms. RTT em milissegundos inteiros. Jitter: média das diferenças absolutas entre tentativas consecutivas com sucesso. Perda: porcentagem sem resposta de sucesso. Ausência de resposta pode ser bloqueio de ICMP; não comprova falha da internet. Não mede download/upload.";
}

public static class NetworkReportBuilder
{
    public static NetworkReport Capture(string version, NetworkInventory? inventory, string? error,
        NetworkLatencyResult? latency, DateTimeOffset? completedAt, string latencyStatus,
        NetworkThroughputResult? throughput = null, string throughputStatus = "Nenhum teste de transferência executado.",
        NetworkDnsResult? dns = null, string dnsStatus = "Nenhuma resolução executada.") => new(
        DateTimeOffset.UtcNow, version, inventory == null ? null : inventory with
        {
            Interfaces = inventory.Interfaces.Select(i => i with { Metrics = i.Metrics.ToArray() }).ToArray(),
            Notices = inventory.Notices.ToArray(),
            Resolver = inventory.Resolver with { Servers = inventory.Resolver.Servers.ToArray() }
        }, error, latency == null ? null : latency with { Probes = latency.Probes.ToArray() },
        latency == null ? null : completedAt, latencyStatus)
        { Throughput = throughput, ThroughputStatus = throughputStatus,
            DnsResolution = dns == null ? null : dns with { Addresses = dns.Addresses.ToArray() },
            DnsResolutionStatus = dnsStatus };

    public static string Json(NetworkReport report) => JsonSerializer.Serialize(report, new JsonSerializerOptions
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    });

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "Indisponível");
    private static string Ms(double? value) => value.HasValue ? value.Value.ToString("0.0", CultureInfo.InvariantCulture) + " ms" : "Indisponível";

    public static string Html(NetworkReport report)
    {
        var html = new StringBuilder("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>RESET EXPLODE · Rede</title><style>body{background:#10131a;color:#e5e7eb;font:16px system-ui;max-width:1000px;margin:32px auto;padding:0 20px}article{background:#1b202b;padding:20px;margin:16px 0;border-radius:16px}p,li{overflow-wrap:anywhere;white-space:pre-wrap}</style></head><body><h1>RESET EXPLODE · Rede</h1>");
        html.Append($"<p>Versão {E(report.AppVersion)} · exportado em {E(report.GeneratedAt.ToString("O"))}</p>");
        html.Append("<p>Snapshot da sessão, sem nova coleta ou teste ao exportar. Contém endereços IP, gateways, DNS e destino do teste. Métricas do inventário preservam o texto e as unidades exibidos na aplicação; contadores são acumulados.</p>");
        if (report.CollectionError != null) html.Append($"<p>Falha de coleta: {E(report.CollectionError)}</p>");
        if (report.Inventory is not { } inventory) html.Append("<p>Nenhuma coleta de rede disponível.</p>");
        else
        {
            html.Append($"<p>Inventário coletado em {E(inventory.Timestamp.ToString("O"))}</p><h2>DNS local</h2><p>{E(inventory.Resolver.Description)}</p>");
            html.Append($"<p>Entradas DNS inválidas ignoradas: {inventory.Resolver.InvalidEntries}</p>");
            if (inventory.Interfaces.Count == 0) html.Append("<p>Nenhuma interface registrada na coleta.</p>");
            foreach (var entry in inventory.Interfaces)
            {
                html.Append($"<article><h2>{E(entry.Name)}</h2>");
                foreach (var metric in entry.Metrics) html.Append($"<p>{E(metric.Name)}: {E(metric.Value)}</p>");
                html.Append("</article>");
            }
            foreach (var notice in inventory.Notices) html.Append($"<p>{E(notice)}</p>");
        }
        html.Append($"<h2>Teste de latência ICMP</h2><p>{E(report.Methodology)}</p><p>{E(report.LatencyStatus)}</p>");
        if (report.Latency is not { } result) html.Append("<p>Nenhum resultado de teste disponível.</p>");
        else
        {
            html.Append($"<p>Destino: {E(result.Target)} · concluído em {E(report.LatencyCompletedAt?.ToString("O"))}</p>");
            html.Append($"<p>Respostas: {result.Received}/{result.Probes.Count} · perda ICMP: {result.LossPercent.ToString("0.0", CultureInfo.InvariantCulture)}%</p>");
            html.Append($"<p>RTT mínimo/médio/máximo: {Ms(result.MinimumMs)} / {Ms(result.AverageMs)} / {Ms(result.MaximumMs)} · jitter: {Ms(result.JitterMs)}</p><ol>");
            foreach (var probe in result.Probes)
                html.Append($"<li>{E(probe.Status.ToString())} · RTT: {Ms(probe.Status == System.Net.NetworkInformation.IPStatus.Success ? probe.RoundtripMilliseconds : null)}</li>");
            html.Append("</ol>");
        }
        html.Append($"<h2>Download / upload HTTP</h2><p>{E(report.ThroughputMethodology)}</p><p>{E(report.ThroughputStatus)}</p>");
        if (report.Throughput is not { } transfer) html.Append("<p>Nenhum resultado de transferência disponível.</p>");
        else
        {
            html.Append($"<p>Servidor: {E(transfer.Server)} · início: {E(transfer.StartedAt.ToString("O"))} · conclusão: {E(transfer.CompletedAt.ToString("O"))}</p>");
            foreach (var (name, measurement) in new[] { ("Download", transfer.Download), ("Upload", transfer.Upload) })
                html.Append($"<p>{name}: {measurement.MegabitsPerSecond.ToString("0.00", CultureInfo.InvariantCulture)} Mbit/s · {measurement.Bytes} bytes em {measurement.Seconds.ToString("0.000", CultureInfo.InvariantCulture)} s</p>");
        }
        html.Append($"<h2>Resolução de nomes pelo sistema</h2><p>{E(report.DnsMethodology)}</p><p>{E(report.DnsResolutionStatus)}</p>");
        if (report.DnsResolution is not { } dns) html.Append("<p>Nenhum resultado de resolução disponível.</p>");
        else
        {
            html.Append($"<p>Nome: {E(dns.Host)} · tempo observado: {dns.ElapsedMilliseconds.ToString("0.00", CultureInfo.InvariantCulture)} ms</p>");
            html.Append($"<p>Início: {E(dns.StartedAt.ToString("O"))} · conclusão: {E(dns.CompletedAt.ToString("O"))}</p>");
            if (dns.Addresses.Count == 0) html.Append("<p>Nenhum endereço retornado.</p>");
            foreach (string address in dns.Addresses) html.Append($"<p>{E(address)}</p>");
        }
        return html.Append("</body></html>").ToString();
    }
}
