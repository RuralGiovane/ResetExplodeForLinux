using System.Diagnostics;
using ResetExplode;

internal static class SingleInstanceChecks
{
    public static int Probe(string key)
    {
        using var instance = new SingleInstance(key, () => Console.WriteLine("ACTIVATED"), Path.Combine(Path.GetTempPath(), key));
        if (!instance.IsPrimary)
        {
            bool activated = instance.RequestActivationAsync().GetAwaiter().GetResult();
            Console.WriteLine(activated ? "SECONDARY" : "NO_RESPONSE");
            return activated ? 0 : 1;
        }
        Console.WriteLine("PRIMARY");
        Console.ReadLine();
        return 0;
    }

    public static async Task RunAsync()
    {
        string key = "instance-test-" + Guid.NewGuid().ToString("N");
        var processes = new List<Process>();
        Process Start(string identity)
        {
            var info = new ProcessStartInfo("dotnet")
            {
                RedirectStandardInput = true, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false
            };
            info.ArgumentList.Add(typeof(SingleInstanceChecks).Assembly.Location);
            info.ArgumentList.Add("--instance-probe");
            info.ArgumentList.Add(identity);
            var process = Process.Start(info)!;
            processes.Add(process);
            return process;
        }
        static async Task Expect(Process process, string expected)
        {
            string? actual = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            if (actual != expected)
                throw new InvalidOperationException($"Instância: esperado {expected}, recebido {actual}. " +
                    (process.HasExited ? await process.StandardError.ReadToEndAsync() : ""));
        }
        static async Task Stop(Process process)
        {
            await process.StandardInput.WriteLineAsync("stop");
            await process.StandardInput.FlushAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            if (process.ExitCode != 0) throw new InvalidOperationException("Instância não encerrou corretamente.");
        }
        try
        {
            var primary = Start(key);
            await Expect(primary, "PRIMARY");
            // Repetição confirma que o listener aceita uma nova conexão após desconectar.
            for (int i = 0; i < 2; i++)
            {
                var secondary = Start(key);
                await Expect(secondary, "SECONDARY");
                await Expect(primary, "ACTIVATED");
                await secondary.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                if (secondary.ExitCode != 0) throw new InvalidOperationException("Segunda instância não notificou.");
            }
            var independent = Start(key + "-other");
            await Expect(independent, "PRIMARY");
            await Stop(independent);
            await Stop(primary);
            var restarted = Start(key);
            await Expect(restarted, "PRIMARY");
            restarted.Kill();
            await restarted.WaitForExitAsync();
            var recovered = Start(key);
            await Expect(recovered, "PRIMARY");
            await Stop(recovered);
            Console.WriteLine("OK: instância única entre processos, ativação repetida, isolamento, reabertura e recuperação após encerramento abrupto.");
        }
        finally
        {
            foreach (var process in processes)
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
                process.Dispose();
            }
            Directory.Delete(Path.Combine(Path.GetTempPath(), key), recursive: true);
            if (Directory.Exists(Path.Combine(Path.GetTempPath(), key + "-other")))
                Directory.Delete(Path.Combine(Path.GetTempPath(), key + "-other"), recursive: true);
        }
    }
}
