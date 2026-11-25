using ResetExplode.Hardware;

internal static class VulkanChecks
{
    private const string Summary = "GPU0:\n apiVersion = 1.3.280\n deviceType = PHYSICAL_DEVICE_TYPE_DISCRETE_GPU\n deviceName = Test GPU\n driverName = Test driver\n deviceUUID = 00112233-4455-6677-8899-aabbccddeeff\n";
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    public static async Task RunAsync()
    {
        var result = LinuxVulkanProbe.Parse("Vulkan Instance Version: 1.3.280\n" + Summary, 0, false);
        Check(result.QuerySucceeded && result.Devices.Count == 1 && result.Devices[0].IsHardwareGpu, "GPU física não reconhecida.");
        Check(result.Devices[0].DriverVersion == null && !result.HasWarnings, "Inventou driver opcional.");
        Check(LinuxVulkanProbe.Parse(Summary.Replace("PHYSICAL_DEVICE_TYPE_", "VK_PHYSICAL_DEVICE_TYPE_"), 0, false).Devices[0].IsHardwareGpu, "Prefixo VK não reconhecido.");
        var software = LinuxVulkanProbe.Parse(Summary.Replace("DISCRETE_GPU", "CPU"), 0, false).Devices[0];
        Check(software.IsSoftware && !software.IsHardwareGpu, "Software virou GPU física.");
        Check(!LinuxVulkanProbe.Parse(Summary.Replace("DISCRETE_GPU", "VIRTUAL_GPU"), 0, false).Devices[0].IsHardwareGpu, "GPU virtual elegível automaticamente.");
        var multi = LinuxVulkanProbe.Parse(Summary + Summary.Replace("GPU0:", "GPU1:").Replace("DISCRETE_GPU", "INTEGRATED_GPU"), 0, true);
        Check(multi.Devices.Count == 2 && multi.HasWarnings, "Perdeu dispositivos/avisos.");
        Check(!LinuxVulkanProbe.Parse(Summary, 1, false).QuerySucceeded, "Publicou saída de processo falho.");
        foreach (var invalid in new[] { "", "Devices:\n", Summary.Replace("apiVersion = 1.3.280", "apiVersion = broken"),
            Summary.Replace("Test GPU", "Bad\u001bGPU"), Summary + "deviceName = Duplicate\n" })
            Check(!LinuxVulkanProbe.Parse(invalid, 0, false).QuerySucceeded, "Saída inválida aceita.");
        var partial = LinuxVulkanProbe.Parse(Summary + "GPU1:\n deviceName = Incomplete\n", 0, false);
        Check(partial.Devices.Count == 1 && partial.HasWarnings, "Saída parcial não sinalizada.");
        Check(LinuxVulkanProbe.Parse(Summary + Summary, 0, false).HasWarnings, "Índice duplicado ignorado.");
        Check(!LinuxVulkanProbe.Parse(new string('x', LinuxVulkanProbe.OutputLimit + 1), 0, false).QuerySucceeded, "Saída ilimitada aceita.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await new LinuxVulkanProbe().ReadAsync(cancelled.Token); throw new InvalidOperationException("Pré-cancelamento ignorado."); }
        catch (OperationCanceledException) { }
        Check(!(await new LinuxVulkanProbe("/nonexistent/vulkaninfo", TimeSpan.FromSeconds(1)).ReadAsync()).QuerySucceeded, "Executável ausente não tratado.");
        if (OperatingSystem.IsLinux()) await ProcessChecks();
        Console.WriteLine("OK: inventário Vulkan, múltiplas GPUs, software, formato inválido, limites, subprocessos e cancelamento.");
    }

    private static async Task ProcessChecks()
    {
        string folder = Directory.CreateTempSubdirectory("reset-vulkan-test-").FullName;
        try
        {
            string script = Path.Combine(folder, "vulkaninfo");
            void Write(string body)
            {
                File.WriteAllText(script, "#!/bin/sh\n[ \"$*\" = '--summary' ] || exit 23\n[ \"$LC_ALL\" = C ] || exit 24\n" + body);
                if (OperatingSystem.IsLinux()) File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            Write("printf '%s' '" + Summary + "'\nprintf 'driver warning' >&2\n");
            var read = await new LinuxVulkanProbe(script, TimeSpan.FromSeconds(3)).ReadAsync();
            Check(read.QuerySucceeded && read.HasWarnings && read.Devices.Count == 1, "Consulta real de subprocesso perdeu stdout/stderr.");
            Write("printf '%s' '" + Summary + "'\nexit 1\n");
            Check(!(await new LinuxVulkanProbe(script, TimeSpan.FromSeconds(3)).ReadAsync()).QuerySucceeded, "Código de saída ignorado.");
            Write("sleep 30\n");
            Check((await new LinuxVulkanProbe(script, TimeSpan.FromMilliseconds(100)).ReadAsync()).Status.Contains("prazo"), "Timeout não tratado.");
            using var cancellation = new CancellationTokenSource(100);
            try { await new LinuxVulkanProbe(script, TimeSpan.FromSeconds(5)).ReadAsync(cancellation.Token); throw new InvalidOperationException("Cancelamento ignorado."); }
            catch (OperationCanceledException) { }
            Write("head -c 300000 /dev/zero\nhead -c 300000 /dev/zero >&2\n");
            Check((await new LinuxVulkanProbe(script, TimeSpan.FromSeconds(3)).ReadAsync()).Status.Contains("limite"), "Limite de pipes não aplicado.");
        }
        finally { Directory.Delete(folder, true); }
    }
}
