using System.Globalization;
using System.Text.Json;
using ResetExplode.Hardware;

internal static class StorageReportChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            var date = DateTimeOffset.Parse("2026-10-22T12:00:00Z", CultureInfo.InvariantCulture);
            var devices = new List<BlockDevice> { new("sda", null, "<script>alert(1)</script>", 9007199254740993L, false, null, false) };
            var notices = new List<string> { "aviso & detalhe" };
            var mounts = new List<MountedFileSystem>
            { new(1, "8:1", "/", "/media/<teste>", "ext4", "/dev/sda1", false, new(1000, 400, 300)) };
            var metrics = new List<SystemEntry> { new("Temperatura", "35 °C") };
            var report = StorageReportBuilder.Capture("0.16", new(date, devices, notices), new(mounts, []), date,
                new("/dev/sda", "LEITURA INCOMPLETA", metrics, notices), date.AddMinutes(-5));
            devices.Clear(); mounts.Clear(); metrics.Clear(); notices.Clear();
            Check(report.Inventory?.Devices.Count == 1 && report.Mounts?.Mounts.Count == 1 && report.Smart?.Metrics.Count == 1,
                "Snapshot compartilha coleções mutáveis.");
            string html = StorageReportBuilder.Html(report);
            Check(!html.Contains("<script>") && html.Contains("&lt;script&gt;") && html.Contains("aviso &amp; detalhe"), "HTML não escapou conteúdo.");
            Check(html.Contains("LEITURA INCOMPLETA") && html.Contains("/media/&lt;teste&gt;"), "Estado ou montagem ausente no HTML.");
            using var json = JsonDocument.Parse(StorageReportBuilder.Json(report));
            var root = json.RootElement;
            Check(root.GetProperty("reportType").GetString() == "linux-storage", "Tipo de relatório ausente.");
            Check(root.GetProperty("inventory").GetProperty("devices")[0].GetProperty("capacityBytes").GetInt64() == 9007199254740993L,
                "JSON perdeu precisão na capacidade.");
            var space = root.GetProperty("mounts").GetProperty("mounts")[0].GetProperty("space");
            Check(space.GetProperty("availableBytes").GetInt64() == 300 && space.GetProperty("freeBytes").GetInt64() == 400,
                "JSON confundiu disponível e livre total.");
            Check(root.GetProperty("smartCollectedAt").GetDateTimeOffset() < root.GetProperty("inventoryCollectedAt").GetDateTimeOffset(),
                "Datas independentes foram perdidas.");
            var empty = StorageReportBuilder.Capture("0.16", null, null, null, null, null);
            Check(StorageReportBuilder.Html(empty).Contains("Nenhuma consulta SMART disponível"), "Ausência virou avaliação SMART.");
            using var emptyJson = JsonDocument.Parse(StorageReportBuilder.Json(empty));
            Check(emptyJson.RootElement.GetProperty("smart").ValueKind == JsonValueKind.Null, "Ausência de SMART não preservada.");
            Console.WriteLine("OK: relatório de armazenamento, snapshot, escaping, precisão e datas independentes.");
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
}
