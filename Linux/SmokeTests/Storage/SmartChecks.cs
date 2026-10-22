using ResetExplode.Hardware;

internal static class SmartChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static async Task RunAsync()
    {
        const string good = """
            {"smart_status":{"passed":true},"temperature":{"current":35},"power_on_time":{"hours":0},"serial_number":"NAO-EXIBIR"}
            """;
        var report = LinuxSmartReader.Parse("/dev/test", good, 0);
        Check(report.Status == "AVALIAÇÃO SMART APROVADA" && report.Metrics.Count == 2, "Avaliação ou métricas válidas perdidas.");
        Check(!string.Join(" ", report.Metrics).Contains("NAO-EXIBIR"), "Número de série vazou para métricas.");
        Check(LinuxSmartReader.Parse("test", "{}", 0).Status == "SEM AVALIAÇÃO SMART", "Ausência de dados virou aprovação.");
        Check(LinuxSmartReader.Parse("test", good, 2).Status == "LEITURA INCOMPLETA", "Falha de acesso virou aprovação.");
        Check(LinuxSmartReader.Parse("test", good, 4).Status == "LEITURA INCOMPLETA", "Falha de comando ignorada.");
        Check(LinuxSmartReader.Parse("test", good, 8).Status == "ALERTA REPORTADO", "Bit de falha SMART ignorado.");
        Check(LinuxSmartReader.Parse("test", good, 16).Status == "ALERTA REPORTADO", "Limite pré-falha ignorado.");
        foreach (int bit in new[] { 32, 64, 128 })
            Check(LinuxSmartReader.Parse("test", good, bit).Status == "ATENÇÃO · REGISTROS/CONTADORES", "Histórico de erros virou aprovação.");
        Check(LinuxSmartReader.Parse("test", "{\"smart_status\":{\"passed\":false}}", 0).Status == "ALERTA REPORTADO", "Falha reportada no JSON ignorada.");
        Check(LinuxSmartReader.Parse("test", "[]", 0).Status == "INDISPONÍVEL", "Raiz JSON inválida foi aceita.");
        Check(LinuxSmartReader.Parse("test", "{", 8).Status == "ALERTA REPORTADO", "JSON inválido ocultou bit de falha.");
        Check(LinuxSmartReader.Parse("test", "{}", 999).Status == "INDISPONÍVEL", "Código inválido aceito.");
        const string nvme = """
            {"smart_status":{"passed":true},"nvme_smart_health_information_log":{"critical_warning":1,"percentage_used":110,"media_errors":2}}
            """;
        var critical = LinuxSmartReader.Parse("test", nvme, 0);
        Check(critical.Status == "ALERTA REPORTADO" && critical.Metrics.Count == 3 && critical.Notices.Count >= 3, "Alertas NVMe perdidos.");
        Check(LinuxSmartReader.Parse("test", "{\"temperature\":{\"current\":-5},\"power_on_time\":{\"hours\":\"inválido\"}}", 0).Metrics.Count == 1,
            "Temperatura negativa válida ou métrica inválida tratada incorretamente.");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        bool stopped = false;
        try { await new LinuxSmartReader().ReadAsync("test", cancelled.Token); }
        catch (OperationCanceledException) { stopped = true; }
        Check(stopped, "Cancelamento prévio ignorado.");
        foreach (string name in new[] { "../sda", ".", "..", "/dev/sda", "", "sda;id" })
        {
            bool rejected = false;
            try { await new LinuxSmartReader().ReadAsync(name); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Nome inválido de dispositivo foi aceito.");
        }
        Console.WriteLine("OK: SMART JSON, bits de saída, dados ausentes, alertas NVMe e cancelamento prévio.");
    }
}
