using System.Diagnostics;
using ResetExplode.Hardware;

internal static class SmartProcessChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static async Task RunAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            Console.WriteLine("SKIP: processos SMART simulados exigem Linux.");
            return;
        }
        string directory = Path.Combine(Path.GetTempPath(), "reset-smart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string executable = Path.Combine(directory, "smartctl simulado");
            async Task SetScript(string body)
            {
                await File.WriteAllTextAsync(executable, "#!/bin/sh\n" + body + "\n");
                File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            var reader = new LinuxSmartReader(executable, TimeSpan.FromSeconds(5));
            await SetScript("""
                [ "$#" = 3 ] && [ "$1" = '--json=c' ] && [ "$2" = '-a' ] && [ "$3" = '/dev/test' ] || exit 1
                printf '%s' '{"smart_status":{"passed":true},"serial_number":"SEGREDO"}'
                printf '%s' 'SEGREDO' >&2
                """);
            var result = await reader.ReadAsync("test");
            Check(result.Status == "AVALIAÇÃO SMART APROVADA" && result.Device == "/dev/test", "Processo não recebeu os argumentos esperados.");
            Check(!string.Join(" ", result.Notices).Contains("SEGREDO"), "stderr foi exposto.");

            await SetScript("printf '%s' '{\"smart_status\":{\"passed\":true}}'\nexit 2");
            Check((await reader.ReadAsync("test")).Status == "LEITURA INCOMPLETA", "Código de saída real não foi preservado.");
            await SetScript("printf '%s' 'JSON inválido'");
            Check((await reader.ReadAsync("test")).Status == "INDISPONÍVEL", "JSON inválido de processo aceito.");
            Check((await new LinuxSmartReader(Path.Combine(directory, "ausente"), TimeSpan.FromSeconds(1)).ReadAsync("test")).Status == "INDISPONÍVEL",
                "Executável ausente não foi tratado.");

            // Simula o lançador administrativo, sem solicitar privilégios reais.
            var privileged = new LinuxSmartReader(executable, TimeSpan.FromSeconds(5),
                ["--disable-internal-agent", "/usr/bin/timeout", "--kill-after=2s", "15s", "/usr/bin/smartctl"], true);
            await SetScript("""
                [ "$#" = 8 ] && [ "$1" = '--disable-internal-agent' ] && [ "$2" = '/usr/bin/timeout' ] && [ "$3" = '--kill-after=2s' ] && [ "$4" = '15s' ] && [ "$5" = '/usr/bin/smartctl' ] && [ "$6" = '--json=c' ] && [ "$7" = '-a' ] && [ "$8" = '/dev/test' ] || exit 1
                printf '%s' '{"smart_status":{"passed":true}}'
                """);
            Check((await privileged.ReadAsync("test")).Status == "AVALIAÇÃO SMART APROVADA", "Argumentos administrativos incorretos.");
            foreach (var (code, status) in new[] { (126, "CANCELADO"), (127, "INDISPONÍVEL"), (124, "INDISPONÍVEL"), (137, "INDISPONÍVEL") })
            {
                await SetScript($"exit {code}");
                Check((await privileged.ReadAsync("test")).Status == status, "Falha de autorização/timeout interpretada como saúde SMART.");
            }
            // Um bitmask SMART válido pode coincidir com os códigos do lançador.
            await SetScript("printf '%s' '{\"smart_status\":{\"passed\":false}}'\nexit 126");
            Check((await privileged.ReadAsync("test")).Status == "ALERTA REPORTADO", "Resposta SMART confundida com cancelamento de autorização.");

            // Enche ambas as pipes em paralelo: uma leitura sequencial pode bloquear.
            foreach (string redirect in new[] { "", " >&2" })
            {
                await SetScript("""
                    head -c 131072 /dev/zero >&2 &
                    head -c 131072 /dev/zero
                    wait
                    """ + "\nhead -c 1048577 /dev/zero" + redirect);
                result = await reader.ReadAsync("test").WaitAsync(TimeSpan.FromSeconds(10));
                Check(result.Notices.Any(n => n.Contains("1 MiB")), "Limite de stdout/stderr não foi aplicado ou pipes bloquearam.");
            }

            // O PID é publicado antes da espera; exec mantém o mesmo PID, sem filho órfão no teste.
            await SetScript("printf '%s' \"$$\" > \"$0.pid\"\nexec sleep 30");
            var timeoutReader = new LinuxSmartReader(executable, TimeSpan.FromSeconds(1));
            result = await timeoutReader.ReadAsync("test").WaitAsync(TimeSpan.FromSeconds(10));
            Check(result.Notices.Any(n => n.Contains("excedeu")), "Timeout não produziu indisponibilidade explícita.");
            AssertExited(executable + ".pid");
            File.Delete(executable + ".pid");

            using var cancellation = new CancellationTokenSource();
            var pending = reader.ReadAsync("test", cancellation.Token);
            try
            {
                using var startupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                while (!File.Exists(executable + ".pid") || new FileInfo(executable + ".pid").Length == 0)
                    await Task.Delay(10, startupDeadline.Token);
            }
            finally { cancellation.Cancel(); }
            bool cancelled = false;
            try { await pending.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "Cancelamento em andamento virou resultado normal.");
            AssertExited(executable + ".pid");
            Console.WriteLine("OK: processo SMART, argumentos, saída/erros, pipes, limite, timeout e cancelamento com encerramento.");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void AssertExited(string pidFile)
    {
        int pid = int.Parse(File.ReadAllText(pidFile));
        try
        {
            using var process = Process.GetProcessById(pid);
            Check(process.HasExited, "Processo SMART permaneceu ativo após o retorno.");
        }
        catch (ArgumentException) { } // PID já recolhido pelo sistema.
    }
}
