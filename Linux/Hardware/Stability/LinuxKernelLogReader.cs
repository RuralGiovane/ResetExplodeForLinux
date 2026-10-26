using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ResetExplode.Hardware;

public sealed record KernelLogEntry(DateTimeOffset Timestamp, int Priority, string Message)
{
    public string? BootId { get; init; }
    public ulong? MonotonicMicroseconds { get; init; }
    public KernelLogClassification Classification => KernelLogClassifier.Classify(Message);
    public string Description
    {
        get
        {
            var classification = Classification;
            return $"{Timestamp.ToLocalTime():dd/MM/yyyy HH:mm:ss.fff} · prioridade {Priority}\n" +
                $"Contexto indicativo: {classification.Label} · {classification.Evidence}\n{Message}";
        }
    }
}

public sealed record KernelLogSnapshot(DateTimeOffset Timestamp, IReadOnlyList<KernelLogEntry> Entries, string Status);

/// <summary>Consulta limitada e manual do journal; mensagens são evidências, não diagnósticos de hardware.</summary>
public sealed class LinuxKernelLogReader
{
    public const int EntryLimit = 500;
    private readonly string _executable;
    private readonly TimeSpan _timeout;
    public LinuxKernelLogReader() : this("journalctl", TimeSpan.FromSeconds(10)) { }
    internal LinuxKernelLogReader(string executable, TimeSpan timeout)
    {
        _executable = executable;
        _timeout = timeout;
    }

    public async Task<KernelLogSnapshot> ReadAsync(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(_timeout);
        using var process = new Process { StartInfo = new(_executable)
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (string arg in new[] { "--kernel", "--boot=0", "--priority=0..4", "--lines=500", "--reverse", "--output=json", "--no-pager" })
            process.StartInfo.ArgumentList.Add(arg);
        process.StartInfo.Environment["LC_ALL"] = "C";
        Task<string>? stdout = null, stderr = null;
        try
        {
            process.Start();
            stdout = ReadBoundedAsync(process.StandardOutput, deadline.Token);
            stderr = ReadBoundedAsync(process.StandardError, deadline.Token);
            await process.WaitForExitAsync(deadline.Token);
            string output = await stdout;
            string notices = await stderr;
            cancellation.ThrowIfCancellationRequested();
            return Parse(output, process.ExitCode, !string.IsNullOrWhiteSpace(notices));
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { return Unavailable("A consulta excedeu o prazo e foi encerrada."); }
        catch (Win32Exception)
        { return Unavailable("Não foi possível iniciar journalctl. Verifique se o sistema usa systemd e se journalctl está instalado."); }
        catch (IOException)
        { return Unavailable("Falha de leitura ou resposta acima do limite de 2 MiB de caracteres."); }
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
        var text = new StringBuilder();
        var buffer = new char[4096];
        bool exceeded = false;
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellation)) > 0)
        {
            if (text.Length + count <= 2 * 1024 * 1024) text.Append(buffer, 0, count);
            else exceeded = true; // Drena ambas as pipes mesmo após o limite.
        }
        if (exceeded) throw new IOException();
        return text.ToString();
    }

    private static KernelLogSnapshot Unavailable(string reason) => new(DateTimeOffset.UtcNow, [], "Logs indisponíveis. " + reason);

    internal static KernelLogSnapshot Parse(string output, int exitCode, bool hasNotices)
    {
        if (exitCode != 0) return Unavailable("journalctl não concluiu a consulta. Verifique acesso ao journal e disponibilidade dos registros.");
        var entries = new List<KernelLogEntry>();
        int invalid = 0, lines = 0;
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (++lines > EntryLimit) break;
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                string? Field(string key) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key, out var value)
                    && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (Field("_TRANSPORT") != "kernel" || string.IsNullOrWhiteSpace(Field("_BOOT_ID"))
                    || !int.TryParse(Field("PRIORITY"), NumberStyles.None, CultureInfo.InvariantCulture, out int priority)
                    || priority is < 0 or > 4
                    || !long.TryParse(Field("__REALTIME_TIMESTAMP"), NumberStyles.None, CultureInfo.InvariantCulture, out long micros)
                    || micros < 0 || Field("MESSAGE") is not { Length: > 0 } message)
                { invalid++; continue; }
                var timestamp = DateTimeOffset.UnixEpoch.AddTicks(checked(micros * 10));
                // JSON pode conter caracteres de controle; não propagá-los ao texto exibido.
                message = new string(message.Select(c => char.IsControl(c) && c != '\n' && c != '\t' ? ' ' : c).ToArray());
                bool shortened = message.Length > 8192;
                if (shortened) message = message[..8192] + "\n[Mensagem truncada]";
                entries.Add(new(timestamp, priority, message)
                {
                    BootId = StabilitySession.NormalizeBootId(Field("_BOOT_ID")),
                    MonotonicMicroseconds = ulong.TryParse(Field("__MONOTONIC_TIMESTAMP"), NumberStyles.None,
                        CultureInfo.InvariantCulture, out ulong monotonic) ? monotonic : null
                });
            }
            catch (Exception ex) when (ex is JsonException or ArgumentOutOfRangeException or OverflowException)
            { invalid++; }
        }
        string status = entries.Count == 0
            ? "Nenhum aviso/erro legível foi retornado. O journal pode estar vazio, indisponível ou com acesso restrito."
            : $"{entries.Count} registros de aviso/erro retornados da inicialização atual (mais recentes primeiro).";
        if (lines >= EntryLimit) status += " Limite de 500 registros atingido; podem existir registros anteriores.";
        if (invalid > 0) status += $" Leitura incompleta: {invalid} registros inválidos ou não textuais ignorados.";
        if (hasNotices) status += " journalctl emitiu avisos; o acesso ou a leitura podem estar incompletos.";
        status += " A consulta não certifica estabilidade nem identifica sozinha falhas de hardware.";
        return new(DateTimeOffset.UtcNow, entries.ToArray(), status);
    }
}
