using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResetExplode.Hardware;

public sealed record StabilityReport(DateTimeOffset GeneratedAt, string AppVersion,
    EdacSnapshot? Edac, string EdacStatus, KernelLogSnapshot? KernelLogs, string KernelLogStatus,
    StabilitySample? Baseline, StabilitySample? Current, KernelLogSnapshot? SessionLogs,
    StabilityComparison? Comparison, string SessionStatus)
{
    public int SchemaVersion => 1;
    public string ReportType => "linux-stability";
    public string ClassificationMethodology => KernelLogClassifier.Methodology;
    public string ComparisonMethodology => "linux-stability-session-v1";
    public string Limitations => "Dados capturados da sessão, sem nova consulta ao exportar. EDAC contém contadores cumulativos; diferenças dependem da continuidade observável. " +
        "Logs: inicialização atual, prioridades 0–4, até 500 registros recentes; mensagens limitadas a 8192 caracteres. " +
        "O intervalo usa o horário monotônico de recebimento no journal, aberto no fim da referência e fechado no início da leitura atual. " +
        "Acesso, retenção e truncamento podem limitar o período. Contextos são indicativos, contagens são de mensagens, não de falhas. " +
        "Reset rápido ou troca com a mesma identidade podem passar despercebidos. Zero ou ausência de dados não certifica estabilidade; não há atribuição causal a benchmarks.";
    public string PrivacyNotice => "Inclui mensagens do kernel, nomes, caminhos, identificadores de dispositivos e da inicialização. Confira antes de compartilhar.";
}

public static class StabilityReportBuilder
{
    private static EdacSnapshot? Copy(EdacSnapshot? source) => source == null ? null : source with { Controllers = source.Controllers.ToArray() };
    private static KernelLogSnapshot? Copy(KernelLogSnapshot? source) => source == null ? null : source with { Entries = source.Entries.ToArray() };
    private static StabilitySample? Copy(StabilitySample? source) => source == null ? null : source with { Edac = Copy(source.Edac)! };

    public static StabilityReport Capture(string version, EdacSnapshot? edac, string edacStatus,
        KernelLogSnapshot? logs, string logStatus, StabilitySample? baseline, StabilitySample? current,
        KernelLogSnapshot? sessionLogs, string sessionStatus)
    {
        var reference = Copy(baseline);
        var latest = Copy(current);
        var journal = Copy(sessionLogs);
        return new(DateTimeOffset.UtcNow, version, Copy(edac), edacStatus, Copy(logs), logStatus,
            reference, latest, journal, reference != null && latest != null && journal != null
                ? StabilitySession.Compare(reference, latest, journal) : null, sessionStatus);
    }

    public static string Json(StabilityReport report) => JsonSerializer.Serialize(report, new JsonSerializerOptions
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    });

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "Indisponível");
    public static string Html(StabilityReport report)
    {
        var html = new StringBuilder("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>RESET EXPLODE · Estabilidade</title><style>body{background:#10131a;color:#e5e7eb;font:16px system-ui;max-width:1000px;margin:32px auto;padding:0 20px}section{background:#1b202b;padding:20px;margin:16px 0;border-radius:16px}p,pre{overflow-wrap:anywhere}pre{white-space:pre-wrap;font:14px monospace}</style></head><body><h1>RESET EXPLODE · Estabilidade</h1>");
        html.Append($"<p>Versão {E(report.AppVersion)} · exportado em {E(report.GeneratedAt.ToString("O"))} · esquema {report.SchemaVersion}</p>");
        html.Append($"<p>{E(report.Limitations)}</p><p>{E(report.PrivacyNotice)}</p>");
        html.Append($"<p>Metodologias: {E(report.ClassificationMethodology)} · {E(report.ComparisonMethodology)}</p>");
        void Text(string text) => html.Append($"<pre>{E(text)}</pre>");
        void Edac(EdacSnapshot? snapshot)
        {
            if (snapshot == null) { Text("Nenhuma leitura EDAC disponível."); return; }
            Text($"Coleta: {snapshot.Timestamp:O}\n{snapshot.Status}");
            foreach (var controller in snapshot.Controllers) Text(controller.Description);
        }
        void Logs(KernelLogSnapshot? snapshot)
        {
            if (snapshot == null) { Text("Nenhuma consulta de logs disponível."); return; }
            Text($"Consulta: {snapshot.Timestamp:O}\n{snapshot.Status}\n{KernelLogClassifier.Summarize(snapshot.Entries)}");
            foreach (var entry in snapshot.Entries) Text($"Data: {entry.Timestamp:O} · boot: {entry.BootId ?? "Indisponível"} · monotônico (µs): {entry.MonotonicMicroseconds?.ToString() ?? "Indisponível"}\n{entry.Description}");
        }
        void Sample(string label, StabilitySample? sample)
        {
            html.Append($"<h3>{E(label)}</h3>");
            if (sample == null) { Text("Nenhuma captura disponível."); return; }
            Text($"Boot: {sample.BootId ?? "Indisponível"} · captura monotônica (µs): {sample.StartMicroseconds} a {sample.EndMicroseconds}");
            Edac(sample.Edac);
        }
        html.Append("<section><h2>EDAC · consulta avulsa</h2>"); Text(report.EdacStatus); Edac(report.Edac); html.Append("</section>");
        html.Append("<section><h2>Kernel · consulta avulsa</h2>"); Text(report.KernelLogStatus); Logs(report.KernelLogs); html.Append("</section>");
        html.Append("<section><h2>Comparação por sessão</h2>"); Text(report.SessionStatus);
        Sample("Referência", report.Baseline); Sample("Leitura posterior", report.Current);
        html.Append("<h3>Diferenças e registros do intervalo</h3>");
        Text(report.Comparison?.Description ?? "Nenhuma comparação disponível.");
        html.Append("<details><summary>Amostra de logs usada na comparação</summary>"); Logs(report.SessionLogs);
        html.Append("</details></section></body></html>");
        return html.ToString();
    }
}
