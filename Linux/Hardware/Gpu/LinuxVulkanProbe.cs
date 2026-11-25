using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ResetExplode.Hardware;

public sealed record VulkanDevice(int Index, string Name, string ApiVersion, string DeviceType,
    string? DriverName, string? DriverVersion, string? DeviceUuid)
{
    // O índice é local à consulta; não é identidade persistente nem índice NVML.
    public bool IsHardwareGpu => DeviceType is "PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU" or "PHYSICAL_DEVICE_TYPE_DISCRETE_GPU";
    public bool IsSoftware => DeviceType == "PHYSICAL_DEVICE_TYPE_CPU";
}

public sealed record VulkanSnapshot(DateTimeOffset Timestamp, IReadOnlyList<VulkanDevice> Devices,
    bool QuerySucceeded, bool HasWarnings, string Status);

/// <summary>Inventário manual, sem carga gráfica. Não comprova suporte ao futuro pipeline de stress.</summary>
public sealed class LinuxVulkanProbe
{
    internal const int OutputLimit = 256 * 1024;
    private readonly string _executable;
    private readonly TimeSpan _timeout;
    public LinuxVulkanProbe() : this("vulkaninfo", TimeSpan.FromSeconds(10)) { }
    internal LinuxVulkanProbe(string executable, TimeSpan timeout)
    { _executable = executable; _timeout = timeout; }

    public async Task<VulkanSnapshot> ReadAsync(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(_timeout);
        using var process = new Process { StartInfo = new(_executable)
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        process.StartInfo.ArgumentList.Add("--summary");
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
            deadline.Token.ThrowIfCancellationRequested();
            return Parse(output, process.ExitCode, !string.IsNullOrWhiteSpace(notices));
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { return Unavailable("A consulta excedeu o prazo e foi encerrada."); }
        catch (Win32Exception)
        { return Unavailable("Não foi possível iniciar vulkaninfo. Verifique a instalação de Vulkan Tools."); }
        catch (IOException)
        { return Unavailable("Falha de leitura ou resposta acima do limite de 256 Ki caracteres por canal."); }
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
            if (text.Length + count <= OutputLimit) text.Append(buffer, 0, count);
            else exceeded = true; // Continuar drenando evita bloquear o processo nas pipes.
        }
        if (exceeded) throw new IOException();
        return text.ToString();
    }

    private static VulkanSnapshot Unavailable(string reason) => new(DateTimeOffset.UtcNow, [], false, false,
        "Inventário Vulkan indisponível. " + reason);

    internal static VulkanSnapshot Parse(string output, int exitCode, bool hasWarnings)
    {
        if (exitCode != 0) return Unavailable("vulkaninfo não concluiu a consulta. Verifique driver, loader e acesso às GPUs.");
        if (output.Length > OutputLimit) return Unavailable("Resposta acima do limite.");
        var devices = new List<VulkanDevice>();
        var indices = new HashSet<int>();
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        int? index = null;
        bool malformed = false, duplicateField = false;
        void Finish()
        {
            if (index == null) return;
            string? Field(string name) => fields.GetValueOrDefault(name);
            string? name = Field("deviceName"), api = Field("apiVersion"), type = Field("deviceType");
            if (duplicateField || string.IsNullOrWhiteSpace(name) || api == null ||
                !Regex.IsMatch(api, @"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant) ||
                type == null || !type.StartsWith("PHYSICAL_DEVICE_TYPE_", StringComparison.Ordinal) ||
                !indices.Add(index.Value))
            { malformed = true; return; }
            string? uuid = Field("deviceUUID");
            if (uuid != null && !Guid.TryParseExact(uuid, "D", out _)) { malformed = true; uuid = null; }
            devices.Add(new(index.Value, name, api, type, Field("driverName"), Field("driverVersion"), uuid));
        }
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } raw)
        {
            string line = raw.Trim();
            if (line.StartsWith("GPU", StringComparison.Ordinal) && line.EndsWith(':'))
            {
                Finish(); fields.Clear(); duplicateField = false; index = null;
                if (int.TryParse(line.AsSpan(3, line.Length - 4), NumberStyles.None, CultureInfo.InvariantCulture, out int value))
                    index = value;
                else malformed = true;
                continue;
            }
            if (index == null) continue;
            int equals = line.IndexOf('=');
            if (equals < 1) continue;
            string key = line[..equals].Trim();
            if (key is not ("deviceName" or "apiVersion" or "deviceType" or "driverName" or "driverVersion" or "deviceUUID")) continue;
            string valueText = line[(equals + 1)..].Trim();
            // Nunca propagar sequências de controle ou campos truncados como identificação válida.
            if (valueText.Length > 512 || valueText.Any(char.IsControl)) { duplicateField = true; continue; }
            if (key == "deviceType" && valueText.StartsWith("VK_", StringComparison.Ordinal)) valueText = valueText[3..];
            if (!fields.TryAdd(key, valueText)) duplicateField = true;
        }
        Finish();
        if (devices.Count == 0) return Unavailable("Nenhum dispositivo legível retornado; a saída pode estar vazia ou em formato não reconhecido.");
        string status = $"{devices.Count} dispositivo(s) Vulkan; {devices.Count(d => d.IsHardwareGpu)} GPU(s) integrada(s)/dedicada(s).";
        if (hasWarnings || malformed) status += " Consulta com avisos ou dados incompletos; outros dispositivos podem estar ausentes.";
        status += " Inventário não valida renderização, proteção térmica ou estabilidade.";
        return new(DateTimeOffset.UtcNow, devices.AsReadOnly(), true, hasWarnings || malformed, status);
    }
}
