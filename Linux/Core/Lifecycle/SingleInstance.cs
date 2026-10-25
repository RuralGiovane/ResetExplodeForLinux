using System.Security.Cryptography;
using System.Text;

namespace ResetExplode;

// Arquivos privados no estado XDG: não depende de sockets nem de privilégios.
public sealed class SingleInstance : IDisposable
{
    private readonly FileStream? _lock;
    private readonly string _directory;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _listener = Task.CompletedTask;
    private bool _disposed;
    public bool IsPrimary { get; }

    public SingleInstance(string applicationId, Action activate, string? stateDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        ArgumentNullException.ThrowIfNull(activate);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Environment.UserName + "\n" + applicationId)))[..32];
        string state = stateDirectory ?? Path.GetDirectoryName(XdgPaths.ForCurrentUser().HistoryFile)!;
        _directory = Path.Combine(state, "instance-" + key);
        if (OperatingSystem.IsLinux()) Directory.CreateDirectory(_directory,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        else Directory.CreateDirectory(_directory);
        try
        {
            try { _lock = OpenPrivate(Path.Combine(_directory, "owner.lock"), FileMode.OpenOrCreate); IsPrimary = true; }
            catch (IOException ex) when ((ex.HResult & 0xffff) is 11 or 32)
            { IsPrimary = false; }
            if (IsPrimary) _listener = ListenAsync(activate);
        }
        catch { _lock?.Dispose(); _stop.Dispose(); throw; }
    }

    private static FileStream OpenPrivate(string path, FileMode mode)
    {
        var options = new FileStreamOptions { Mode = mode, Access = FileAccess.ReadWrite, Share = FileShare.None };
        if (OperatingSystem.IsLinux()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(path, options);
    }

    public async Task<bool> RequestActivationAsync()
    {
        if (IsPrimary) throw new InvalidOperationException("A instância principal não deve notificar a si mesma.");
        string request = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".request");
        string ack = Path.ChangeExtension(request, ".ack");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            using (OpenPrivate(request, FileMode.CreateNew)) { }
            while (!File.Exists(ack)) await Task.Delay(50, deadline.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException)
        { return false; }
        finally { TryDelete(request); TryDelete(ack); }
    }

    private async Task ListenAsync(Action activate)
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                // Pedidos vazios só significam ativar a janela, nunca contêm comandos.
                foreach (string request in Directory.EnumerateFiles(_directory, "*.request"))
                {
                    if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(request), "N", out _)) continue;
                    try
                    {
                        if (DateTime.UtcNow - File.GetLastWriteTimeUtc(request) > TimeSpan.FromMinutes(1))
                        { TryDelete(request); continue; }
                        activate();
                        File.Move(request, Path.ChangeExtension(request, ".ack"), overwrite: true);
                    }
                    catch (IOException) { } // O cliente pode ter desistido.
                }
                foreach (string ack in Directory.EnumerateFiles(_directory, "*.ack"))
                    if (DateTime.UtcNow - File.GetLastWriteTimeUtc(ack) > TimeSpan.FromMinutes(1)) TryDelete(ack);
                await Task.Delay(200, _stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                try { await Task.Delay(200, _stop.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stop.Cancel();
        try { _listener.GetAwaiter().GetResult(); }
        finally
        {
            // Nunca apagar owner.lock: todos os processos devem bloquear o mesmo inode.
            _lock?.Dispose();
            _stop.Dispose();
        }
    }
}
