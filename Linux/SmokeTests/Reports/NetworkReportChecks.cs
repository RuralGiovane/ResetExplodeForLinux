using System.Net.NetworkInformation;
using System.Text.Json;
using ResetExplode.Hardware;

internal static class NetworkReportChecks
{
    public static void Run()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        var collected = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        var completed = collected.AddMinutes(1);
        var metrics = new List<SystemEntry> { new("IPv6 <local>", "fe80::1%7/64"), new("Contador", "18.446.744.073.709.551.615") };
        var entries = new List<NetworkInterfaceEntry> { new("<script>eth0</script>", metrics) };
        var notices = new List<string> { "Aviso & teste" };
        var servers = new List<string> { "127.0.0.53", "::1" };
        var probes = new List<NetworkProbe> { new(IPStatus.Success, 0), new(IPStatus.Success, 20) };
        probes.AddRange(Enumerable.Repeat(new NetworkProbe(IPStatus.TimedOut, 0), 8));
        var report = NetworkReportBuilder.Capture("<v1>", new(collected, entries, notices, new(true, servers, 1)),
            null, new("fe80::1%7", probes), completed, "Teste concluído.");
        metrics.Clear(); entries.Clear(); notices.Clear(); servers.Clear(); probes.Clear();
        Check(report.Inventory!.Interfaces.Single().Metrics.Count == 2 && report.Inventory.Resolver.Servers.Count == 2 && report.Inventory.Notices.Count == 1 && report.Latency!.Probes.Count == 10, "Snapshot de rede compartilhou coleções.");
        string html = NetworkReportBuilder.Html(report);
        Check(!html.Contains("<script>") && html.Contains("&lt;script&gt;") && html.Contains("Aviso &amp; teste") && html.Contains("&lt;v1&gt;"), "HTML não escapou dados.");
        Check(html.Contains("fe80::1%7/64") && html.Contains("18.446.744.073.709.551.615") && html.Contains("20.0 ms") && html.Contains("80.0%"), "HTML perdeu métricas.");
        using var json = JsonDocument.Parse(NetworkReportBuilder.Json(report));
        var root = json.RootElement;
        Check(root.GetProperty("reportType").GetString() == "linux-network" && root.GetProperty("schemaVersion").GetInt32() == 3, "Identidade incorreta.");
        Check(root.GetProperty("inventory").GetProperty("timestamp").GetDateTimeOffset() == collected && root.GetProperty("latencyCompletedAt").GetDateTimeOffset() == completed && root.GetProperty("generatedAt").GetDateTimeOffset() == report.GeneratedAt, "Datas confundidas.");
        var latency = root.GetProperty("latency");
        Check(latency.GetProperty("minimumMs").GetDouble() == 0 && latency.GetProperty("averageMs").GetDouble() == 10 && latency.GetProperty("jitterMs").GetDouble() == 20 && latency.GetProperty("lossPercent").GetDouble() == 80, "Métricas JSON inválidas.");
        Check(latency.GetProperty("probes")[2].GetProperty("status").GetString() == "TimedOut", "Estado de tentativa perdido.");
        foreach (string status in new[] { "Nenhum teste executado.", "Teste cancelado.", "Erro <local>" })
        {
            var absent = NetworkReportBuilder.Capture("1", null, "Falha <coleta>", null, completed, status);
            using var absentJson = JsonDocument.Parse(NetworkReportBuilder.Json(absent));
            Check(absentJson.RootElement.GetProperty("latency").ValueKind == JsonValueKind.Null && absentJson.RootElement.GetProperty("latencyCompletedAt").ValueKind == JsonValueKind.Null, "Ausência exportou resultado ou data antiga.");
            Check(absentJson.RootElement.GetProperty("latencyStatus").GetString() == status, "Estado ausente perdido.");
            string absentHtml = NetworkReportBuilder.Html(absent);
            Check(absentHtml.Contains("Nenhuma coleta de rede disponível") && absentHtml.Contains("Nenhum resultado de teste disponível") && absentHtml.Contains("Falha &lt;coleta&gt;") && !absentHtml.Contains("Erro <local>"), "Ausência/falha HTML incorreta.");
        }
        var empty = NetworkReportBuilder.Capture("1", new(collected, [], [], new(false, [], 0)), null,
            new("127.0.0.1", Enumerable.Repeat(new NetworkProbe(IPStatus.TimedOut, 0), 10).ToArray()), completed, "Teste concluído.");
        Check(NetworkReportBuilder.Html(empty).Contains("Nenhuma interface registrada"), "Inventário vazio confundido com ausência.");
        using var emptyJson = JsonDocument.Parse(NetworkReportBuilder.Json(empty));
        Check(emptyJson.RootElement.GetProperty("latency").GetProperty("averageMs").ValueKind == JsonValueKind.Null && emptyJson.RootElement.GetProperty("latency").GetProperty("jitterMs").ValueKind == JsonValueKind.Null && !emptyJson.RootElement.GetProperty("inventory").GetProperty("resolver").GetProperty("available").GetBoolean(), "Indisponibilidade virou zero.");
        var transfer = new NetworkThroughputResult(collected, completed, new(10_000_000, 2.5), new(5_000_000, 4));
        var withTransfer = report with { Throughput = transfer, ThroughputStatus = "Concluído <teste>" };
        using var transferJson = JsonDocument.Parse(NetworkReportBuilder.Json(withTransfer));
        var exported = transferJson.RootElement.GetProperty("throughput");
        Check(exported.GetProperty("download").GetProperty("megabitsPerSecond").GetDouble() == 32 && exported.GetProperty("upload").GetProperty("megabitsPerSecond").GetDouble() == 10 && exported.GetProperty("completedAt").GetDateTimeOffset() == completed, "Transferência perdeu taxas ou datas.");
        string transferHtml = NetworkReportBuilder.Html(withTransfer);
        Check(transferHtml.Contains("32.00 Mbit/s") && transferHtml.Contains("speed.cloudflare.com") && transferHtml.Contains("&lt;teste&gt;") && !transferHtml.Contains("Concluído <teste>"), "HTML de transferência incorreto.");
        Check(root.GetProperty("throughput").ValueKind == JsonValueKind.Null && NetworkReportBuilder.Html(report).Contains("Nenhum resultado de transferência disponível"), "Ausência de transferência virou resultado.");
        var dnsAddresses = new List<string> { "192.0.2.1", "fe80::1%7" };
        var dnsReport = NetworkReportBuilder.Capture("1", null, null, null, null, "Ausente", dns: new("<host>", collected, completed, 12.5, dnsAddresses), dnsStatus: "Concluído <dns>");
        dnsAddresses.Clear();
        using var dnsJson = JsonDocument.Parse(NetworkReportBuilder.Json(dnsReport));
        var dnsRoot = dnsJson.RootElement.GetProperty("dnsResolution");
        Check(dnsRoot.GetProperty("addresses").GetArrayLength() == 2 && dnsRoot.GetProperty("elapsedMilliseconds").GetDouble() == 12.5 && dnsRoot.GetProperty("startedAt").GetDateTimeOffset() == collected, "Relatório DNS perdeu snapshot, duração ou data.");
        string dnsHtml = NetworkReportBuilder.Html(dnsReport);
        Check(dnsHtml.Contains("&lt;host&gt;") && dnsHtml.Contains("&lt;dns&gt;") && dnsHtml.Contains("12.50 ms") && dnsHtml.Contains("fe80::1%7"), "HTML DNS incorreto.");
        Check(root.GetProperty("dnsResolution").ValueKind == JsonValueKind.Null && NetworkReportBuilder.Html(report).Contains("Nenhum resultado de resolução disponível"), "Ausência DNS virou resultado.");
        Console.WriteLine("OK: relatório de rede, snapshot, escaping, estados, datas, métricas e ausência de respostas.");
    }
}
