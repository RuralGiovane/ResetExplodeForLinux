using System.Text.Json;
using ResetExplode.Hardware;

internal static class KernelLogChecks
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static string Entry(string message = "PCIe warning", string priority = "4", string timestamp = "1700000000123456") =>
        JsonSerializer.Serialize(new Dictionary<string, string> { ["_TRANSPORT"] = "kernel", ["_BOOT_ID"] = "test-boot",
            ["PRIORITY"] = priority, ["__REALTIME_TIMESTAMP"] = timestamp, ["MESSAGE"] = message });

    public static async Task RunAsync()
    {
        var valid = LinuxKernelLogReader.Parse(Entry() + "\n" + Entry("GPU timeout", "3"), 0, false);
        Check(valid.Entries.Count == 2 && valid.Entries[0].Priority == 4 && valid.Entries[1].Message == "GPU timeout", "Perdeu registros/prioridades.");
        Check(valid.Entries[0].Timestamp.ToUnixTimeMilliseconds() == 1700000000123, "Timestamp incorreto.");
        Check(LinuxKernelLogReader.Parse(Entry(), 1, false).Entries.Count == 0, "Publicou saída de comando com erro.");
        Check(LinuxKernelLogReader.Parse("", 0, false).Status.Contains("acesso restrito"), "Vazio virou certificado.");
        Check(LinuxKernelLogReader.Parse(Entry(), 0, true).Status.Contains("incompletos"), "Ignorou avisos do comando.");
        string malformed = string.Join('\n', "not json", "[]", Entry(priority: "6"), Entry(timestamp: long.MaxValue.ToString()),
            Entry().Replace("\"kernel\"", "\"syslog\""), "{\"MESSAGE\":[65,66]}", Entry());
        var partial = LinuxKernelLogReader.Parse(malformed, 0, false);
        Check(partial.Entries.Count == 1 && partial.Status.Contains("6 registros inválidos"), "Dados inválidos não sinalizados.");
        var limited = LinuxKernelLogReader.Parse(string.Join('\n', Enumerable.Repeat(Entry(), 501)), 0, false);
        Check(limited.Entries.Count == 500 && limited.Status.Contains("Limite"), "Limite de registros falhou.");
        var clean = LinuxKernelLogReader.Parse(Entry("hello\u001bworld"), 0, false);
        Check(!clean.Entries[0].Message.Contains('\u001b'), "Controle propagado.");
        Check(LinuxKernelLogReader.Parse(Entry(new string('a', 9000)), 0, false).Entries[0].Message.EndsWith("[Mensagem truncada]"), "Truncamento silencioso.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await new LinuxKernelLogReader().ReadAsync(cancelled.Token); throw new InvalidOperationException("Ignorou pré-cancelamento."); }
        catch (OperationCanceledException) { }
        var missing = await new LinuxKernelLogReader("/nonexistent/reset-journalctl", TimeSpan.FromSeconds(1)).ReadAsync();
        Check(missing.Status.Contains("indisponíveis"), "Executável ausente não tratado.");
        if (OperatingSystem.IsLinux()) await ProcessChecks();
        Console.WriteLine("OK: logs do kernel, parsing, limites, indisponibilidade, subprocessos e cancelamento.");
    }

    private static async Task ProcessChecks()
    {
        string folder = Directory.CreateTempSubdirectory("reset-journal-test-").FullName;
        try
        {
            string script = Path.Combine(folder, "journalctl");
            void Write(string body)
            {
                File.WriteAllText(script, "#!/bin/sh\n" +
                    "[ \"$*\" = '--kernel --boot=0 --priority=0..4 --lines=500 --reverse --output=json --no-pager' ] || exit 23\n" + body);
                if (OperatingSystem.IsLinux()) File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            Write("printf '%s\\n' '" + Entry() + "'\n");
            var read = await new LinuxKernelLogReader(script, TimeSpan.FromSeconds(2)).ReadAsync();
            Check(read.Entries.Count == 1, "Integração stdout falhou.");
            Write("printf '%s\\n' '" + Entry() + "'\nprintf 'access warning' >&2\n");
            Check((await new LinuxKernelLogReader(script, TimeSpan.FromSeconds(2)).ReadAsync()).Status.Contains("incompletos"), "Stderr ignorado.");
            Write("exit 1\n");
            Check((await new LinuxKernelLogReader(script, TimeSpan.FromSeconds(2)).ReadAsync()).Status.Contains("indisponíveis"), "Exit code ignorado.");
            Write("sleep 30\n");
            Check((await new LinuxKernelLogReader(script, TimeSpan.FromMilliseconds(100)).ReadAsync()).Status.Contains("prazo"), "Timeout falhou.");
            using var cancellation = new CancellationTokenSource(100);
            try { await new LinuxKernelLogReader(script, TimeSpan.FromSeconds(5)).ReadAsync(cancellation.Token); throw new InvalidOperationException("Cancelamento ignorado."); }
            catch (OperationCanceledException) { }
            Write("head -c 2200000 /dev/zero\n");
            Check((await new LinuxKernelLogReader(script, TimeSpan.FromSeconds(3)).ReadAsync()).Status.Contains("limite"), "Saída ilimitada.");
        }
        finally { Directory.Delete(folder, true); }
    }
}
