using System.Text.Json;
using ResetExplode.Hardware;

internal static class SerialReportChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        var date = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
        var aliases = new List<string> { "/dev/serial/by-id/<script>serial-123</script>" };
        var ports = new List<SerialPortEntry> { new("ttyUSB0", true, false, "188:0")
        {
            Aliases = aliases,
            Usb = new("/sys/devices/<usb>", "1A86", "7523", "Marca & nome", "<img src=x>", "ch341", "/sys/devices/interface")
        } };
        var groups = new List<SerialGroupEntry> { new("dialout", 20, false), new("uucp", null, null) };
        var notices = new List<string> { "Aviso <teste>" };
        var report = SerialReportBuilder.Capture("0.16 <teste>", new(date, ports, groups, notices));
        aliases.Clear(); ports.Clear(); groups.Clear(); notices.Clear();
        Check(report.Inventory?.Ports.Single().Aliases.Count == 1 && report.Inventory.Groups.Count == 2 && report.Inventory.Notices.Count == 1,
            "Snapshot serial compartilhou coleções.");
        string html = SerialReportBuilder.Html(report);
        Check(!html.Contains("<script>") && !html.Contains("<img src=x>") && html.Contains("&lt;script&gt;") && html.Contains("Marca &amp; nome"), "HTML serial não escapou dados.");
        Check(html.Contains("serial-123") && html.Contains("números de série"), "Alias ou informação sobre identificadores foi perdida.");
        using var json = JsonDocument.Parse(SerialReportBuilder.Json(report));
        var root = json.RootElement;
        Check(root.GetProperty("reportType").GetString() == "linux-serial" && root.GetProperty("schemaVersion").GetInt32() == 1, "Tipo/versão serial incorretos.");
        Check(root.GetProperty("generatedAt").GetDateTimeOffset() == report.GeneratedAt && root.GetProperty("inventory").GetProperty("timestamp").GetDateTimeOffset() == date, "Datas de exportação/coleta perdidas.");
        var port = root.GetProperty("inventory").GetProperty("ports")[0];
        Check(port.GetProperty("listedInSysfs").GetBoolean() && !port.GetProperty("listedInDev").GetBoolean(), "Fontes de presença confundidas.");
        Check(port.GetProperty("usb").GetProperty("vendorId").GetString() == "1A86" && port.GetProperty("aliases")[0].GetString()!.Contains("serial-123"), "Identificação/aliases perdidos no JSON.");
        var exportedGroups = root.GetProperty("inventory").GetProperty("groups");
        Check(!exportedGroups[0].GetProperty("active").GetBoolean() && exportedGroups[1].GetProperty("active").ValueKind == JsonValueKind.Null, "Grupo ausente virou false.");
        Check(SerialReportBuilder.Html(SerialReportBuilder.Capture("0.16", null)).Contains("Nenhuma coleta serial disponível"), "Ausência de coleta incorreta.");
        Check(SerialReportBuilder.Html(SerialReportBuilder.Capture("0.16", new(date, [], [], []))).Contains("Nenhuma porta registrada"), "Coleta vazia incorreta.");
        var failed = SerialReportBuilder.Capture("0.16", null, "Erro <leitura>");
        Check(SerialReportBuilder.Html(failed).Contains("Erro &lt;leitura&gt;"), "Falha não foi escapada.");
        using var failure = JsonDocument.Parse(SerialReportBuilder.Json(failed));
        Check(failure.RootElement.GetProperty("inventory").ValueKind == JsonValueKind.Null && failure.RootElement.GetProperty("collectionError").GetString() == "Erro <leitura>", "Falha JSON incorreta.");
        Console.WriteLine("OK: relatório Maker, snapshot, escaping, aliases, identidade, grupos, datas, ausência e falha.");
    }
}
