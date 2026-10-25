using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ResetExplode.SmokeTests")]

namespace ResetExplode.Hardware;

public sealed record SmartReport(string Device, string Status, IReadOnlyList<SystemEntry> Metrics, IReadOnlyList<string> Notices);

public sealed class LinuxSmartReader
{
    private readonly string _executable;
    private readonly TimeSpan _timeout;
    private readonly string[] _prefix;
    private readonly bool _privileged;

    public static LinuxSmartReader CreatePrivileged()
    {
        string smartctl = new[] { "/usr/sbin/smartctl", "/usr/bin/smartctl", "/sbin/smartctl" }
            .FirstOrDefault(File.Exists) ?? throw new InvalidOperationException("Instale smartmontools para consultar SMART.");
        if (!File.Exists("/usr/bin/pkexec") || !File.Exists("/usr/bin/timeout"))
            throw new InvalidOperationException("A consulta administrativa exige polkit (pkexec) e coreutils (timeout).");
        // O timeout elevado pode encerrar a consulta mesmo sem privilégios no processo da UI.
        return new("/usr/bin/pkexec", TimeSpan.FromMinutes(2),
            ["--disable-internal-agent", "/usr/bin/timeout", "--kill-after=2s", "15s", smartctl], true);
    }

    public LinuxSmartReader() : this("smartctl", TimeSpan.FromSeconds(15)) { }

    internal LinuxSmartReader(string executable, TimeSpan timeout, string[]? prefix = null, bool privileged = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        _executable = executable;
        _timeout = timeout;
        _prefix = prefix ?? [];
        _privileged = privileged;
    }

    public async Task<SmartReport> ReadAsync(string name, CancellationToken cancellation = default)
    {
        if (string.IsNullOrEmpty(name) || name is "." or ".." || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new ArgumentException("Nome de dispositivo inválido.", nameof(name));
        string device = "/dev/" + name;
        cancellation.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(_timeout);
        using var process = new Process { StartInfo = new ProcessStartInfo(_executable)
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (string arg in _prefix) process.StartInfo.ArgumentList.Add(arg);
        foreach (string arg in new[] { "--json=c", "-a", device }) process.StartInfo.ArgumentList.Add(arg);
        Task<string>? stdout = null, stderr = null;
        try
        {
            process.Start();
            stdout = ReadBoundedAsync(process.StandardOutput, deadline.Token);
            stderr = ReadBoundedAsync(process.StandardError, deadline.Token);
            await process.WaitForExitAsync(deadline.Token);
            string output = await stdout;
            await stderr; // Drenar sem expor saída bruta, que pode conter identificadores do dispositivo.
            if (_privileged && string.IsNullOrWhiteSpace(output))
            {
                if (process.ExitCode == 126)
                    return new(device, "CANCELADO", [], ["Autorização cancelada. Nenhuma avaliação SMART obtida."]);
                if (process.ExitCode == 127)
                    return new(device, "INDISPONÍVEL", [], ["Autorização não obtida ou comando indisponível. Verifique o agente de autenticação polkit da sessão."]);
                if (process.ExitCode is 124 or 137)
                    return new(device, "INDISPONÍVEL", [], ["A consulta administrativa excedeu o prazo e foi encerrada."]);
            }
            return Parse(device, output, process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            if (cancellation.IsCancellationRequested) throw;
            return new(device, "INDISPONÍVEL", [], [$"A consulta excedeu {_timeout.TotalSeconds:g} segundos e foi cancelada."]);
        }
        catch (Win32Exception)
        { return new(device, "INDISPONÍVEL", [], ["Não foi possível iniciar smartctl. Verifique a instalação do smartmontools e o PATH."]); }
        catch (IOException ex)
        { return new(device, "INDISPONÍVEL", [], [$"Falha ao ler a resposta: {ex.Message}"]); }
        finally
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
            deadline.Cancel();
            if (stdout != null) { try { await stdout; } catch (Exception) { } }
            if (stderr != null) { try { await stderr; } catch (Exception) { } }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellation)
    {
        const int limit = 1024 * 1024;
        var result = new StringBuilder();
        var buffer = new char[4096];
        int count;
        bool exceeded = false;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellation)) > 0)
        {
            if (result.Length + count <= limit) result.Append(buffer, 0, count);
            else exceeded = true; // Continuar drenando evita bloquear o processo nas pipes.
        }
        if (exceeded) throw new IOException("Resposta maior que o limite de 1 MiB de caracteres.");
        return result.ToString();
    }

    public static SmartReport Parse(string device, string json, int exitCode)
    {
        var notices = new List<string>();
        var metrics = new List<SystemEntry>();
        if (exitCode is < 0 or > 255) return new(device, "INDISPONÍVEL", [], ["Código de saída inválido."]);
        string[] bits = ["Erro de argumentos ou falha interna do smartctl.",
            "Dispositivo não pôde ser aberto/identificado. Verifique permissões e suporte do controlador.",
            "Uma operação SMART falhou ou houve erro de checksum; a leitura pode estar incompleta.",
            "O dispositivo reportou falha na avaliação SMART.",
            "Atributo pré-falha atingiu o limite reportado pelo dispositivo.",
            "Há registro de atributo que atingiu limite no passado.",
            "O log do dispositivo contém erros.", "O log de autotestes contém erros."];
        for (int bit = 0; bit < 8; bit++) if ((exitCode & (1 << bit)) != 0) notices.Add(bits[bit]);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new JsonException();
            JsonElement Field(JsonElement obj, string key) => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var v) ? v : default;
            decimal? Number(JsonElement obj, string key) => Field(obj, key) is var v && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var n) ? n : null;
            void Add(string label, JsonElement obj, string key, string unit = "", bool signed = false)
            { if (Number(obj, key) is decimal value && (signed || value >= 0)) metrics.Add(new(label, $"{value:N0}{unit}")); }
            var passedField = Field(Field(root, "smart_status"), "passed");
            bool? passed = passedField.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null };
            Add("Temperatura", Field(root, "temperature"), "current", " °C", signed: true);
            Add("Horas ligado", Field(root, "power_on_time"), "hours", " h");
            Add("Ciclos de energia", root, "power_cycle_count");
            var nvme = Field(root, "nvme_smart_health_information_log");
            decimal? critical = Number(nvme, "critical_warning");
            decimal? used = Number(nvme, "percentage_used");
            decimal? errors = Number(nvme, "media_errors");
            Add("Alerta crítico NVMe (máscara)", nvme, "critical_warning");
            Add("Estimativa de uso da endurance NVMe", nvme, "percentage_used", "%");
            Add("Reserva disponível NVMe", nvme, "available_spare", "%");
            Add("Erros de mídia NVMe", nvme, "media_errors");
            if (critical > 0) notices.Add("O dispositivo reporta bits de alerta crítico NVMe.");
            if (used >= 100) notices.Add("A estimativa de endurance consumida reportada pelo NVMe atingiu 100% ou mais.");
            if (errors > 0) notices.Add("O NVMe registra erros de mídia/integridade; o contador não indica quando ocorreram.");
            bool failed = passed == false || (exitCode & 24) != 0 || critical > 0;
            bool warning = (exitCode & 224) != 0 || used >= 100 || errors > 0;
            string status = failed ? "ALERTA REPORTADO" : warning ? "ATENÇÃO · REGISTROS/CONTADORES"
                : (exitCode & 7) != 0 ? "LEITURA INCOMPLETA"
                : passed == true ? "AVALIAÇÃO SMART APROVADA" : "SEM AVALIAÇÃO SMART";
            if (passed == null) notices.Add("O dispositivo não forneceu uma avaliação SMART válida.");
            notices.Add("Uma avaliação aprovada não garante ausência de falhas. Métricas ausentes não são tratadas como zero.");
            return new(device, status, metrics, notices);
        }
        catch (JsonException)
        {
            notices.Add("Resposta JSON ausente ou inválida.");
            return new(device, (exitCode & 24) != 0 ? "ALERTA REPORTADO" : "INDISPONÍVEL", [], notices);
        }
    }
}
