using System.Text.Json;
using ResetExplode.Hardware;

internal static class PowerReportChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        var date = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
        var metrics = new List<SystemEntry> { new("Energia <atual>", "12,50 Wh"), new("Carga", "0%"), new("Modelo", "<script>x</script>") };
        var supplies = new List<PowerSupply> { new("BAT0", "Battery", "Sistema", metrics) };
        var notices = new List<string> { "Aviso & detalhe" };
        var report = PowerReportBuilder.Capture("0.16", new(date, supplies, notices));
        metrics.Clear(); supplies.Clear(); notices.Clear();
        Check(report.Inventory?.Supplies.Single().Metrics.Count == 3 && report.Inventory.Notices.Count == 1,
            "Snapshot compartilhou coleções internas.");
        string html = PowerReportBuilder.Html(report);
        Check(!html.Contains("<script>") && html.Contains("&lt;script&gt;") && html.Contains("Aviso &amp; detalhe"), "HTML não escapou os textos.");
        Check(html.Contains("12,50 Wh") && html.Contains("0%"), "Valores da coleta foram alterados.");
        using var json = JsonDocument.Parse(PowerReportBuilder.Json(report));
        Check(json.RootElement.GetProperty("reportType").GetString() == "linux-power", "Tipo do relatório incorreto.");
        var inventory = json.RootElement.GetProperty("inventory");
        Check(inventory.GetProperty("timestamp").GetDateTimeOffset() == date, "Data de coleta perdida.");
        Check(inventory.GetProperty("supplies")[0].GetProperty("metrics")[0].GetProperty("value").GetString() == "12,50 Wh",
            "JSON perdeu unidade ou apresentação original.");
        var missing = PowerReportBuilder.Capture("0.16", null);
        Check(PowerReportBuilder.Html(missing).Contains("Nenhuma coleta de alimentação disponível"), "Ausência de coleta virou inventário vazio.");
        var empty = PowerReportBuilder.Capture("0.16", new(date, [], ["Sem fontes"]));
        Check(PowerReportBuilder.Html(empty).Contains("Nenhuma fonte/bateria registrada"), "Coleta vazia foi confundida com ausência de coleta.");
        var failed = PowerReportBuilder.Capture("0.16", null, "Falha <simulada>");
        Check(PowerReportBuilder.Html(failed).Contains("Falha &lt;simulada&gt;"), "Falha não foi preservada/escapada.");
        using var failedJson = JsonDocument.Parse(PowerReportBuilder.Json(failed));
        Check(failedJson.RootElement.GetProperty("inventory").ValueKind == JsonValueKind.Null &&
            failedJson.RootElement.GetProperty("collectionError").GetString() == "Falha <simulada>", "JSON de falha incorreto.");
        Console.WriteLine("OK: relatório de alimentação, snapshot profundo, escaping, valores, datas, ausência e falha.");
    }
}
