using System.Net;
using System.Net.Sockets;

namespace ResetExplode.Hardware;

public sealed record ResolverConfiguration(bool Available, IReadOnlyList<string> Servers, int InvalidEntries)
{
    public string Description => !Available ? "Indisponível" : Servers.Count == 0
        ? "Nenhum servidor explícito válido informado" : string.Join(Environment.NewLine, Servers);
}

public static class LinuxResolverConfiguration
{
    // Descreve somente entradas explícitas do arquivo, não o DNS efetivamente usado por cada app.
    public static ResolverConfiguration Parse(string? content)
    {
        if (content == null) return new(false, Array.AsReadOnly(Array.Empty<string>()), 0);
        var servers = new List<string>();
        int invalid = 0;
        foreach (string line in content.Split('\n'))
        {
            string body = line.Split(['#', ';'], 2)[0];
            string[] fields = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 0 || fields[0] != "nameserver") continue;
            if (fields.Length != 2 || !IPAddress.TryParse(fields[1], out var address) ||
                (address.AddressFamily == AddressFamily.InterNetwork && fields[1] != address.ToString()) ||
                fields[1].Contains('[') || fields[1].Contains(']'))
            { invalid++; continue; }
            string normalized = address.ToString();
            if (!servers.Contains(normalized, StringComparer.Ordinal)) servers.Add(normalized);
        }
        return new(true, servers.AsReadOnly(), invalid);
    }
}
