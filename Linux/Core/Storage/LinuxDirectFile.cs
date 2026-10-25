using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ResetExplode;

internal interface IDirectFile : IDisposable
{
    void Read(AlignedBuffer buffer, int count, long offset, CancellationToken ct);
    void Write(AlignedBuffer buffer, int count, long offset, CancellationToken ct);
}

internal sealed unsafe class AlignedBuffer : IDisposable
{
    private void* _pointer;
    public int Length { get; }
    public Span<byte> Span => _pointer == null ? throw new ObjectDisposedException(nameof(AlignedBuffer)) : new(_pointer, Length);
    public nint Pointer => (nint)_pointer;
    public AlignedBuffer(int length)
    {
        if (length <= 0 || length % 4096 != 0) throw new ArgumentOutOfRangeException(nameof(length));
        Length = length;
        _pointer = NativeMemory.AlignedAlloc((nuint)length, 4096);
        if (_pointer == null) throw new OutOfMemoryException();
    }
    public void Dispose() { NativeMemory.AlignedFree(_pointer); _pointer = null; }
}

/// <summary>ABI Linux x64. Nunca reabre sem O_DIRECT nem aceita transferências incompletas.</summary>
internal sealed class LinuxDirectFile : IDirectFile
{
    private readonly SafeFileHandle _handle;
    private const int Direct = 0x4000, Dsync = 0x1000, Cloexec = 0x80000, NoFollow = 0x20000;
    private const uint DioAlign = 0x2000;

    public LinuxDirectFile(string path, bool create)
    {
        RequirePlatform();
        int fd = Open(path, Direct | Cloexec | NoFollow | (create ? 2 | 0x40 | 0x80 | Dsync : 0), 0x180); // 0600
        if (fd < 0) throw Error("Não foi possível abrir arquivo com O_DIRECT", path);
        _handle = new SafeFileHandle((nint)fd, ownsHandle: true);
        try
        {
            if (Fstatfs(_handle, out var filesystem) != 0) throw Error("Sistema de arquivos indisponível", path);
            if (filesystem.Type == 0x9123683E) // BTRFS_SUPER_MAGIC
            {
                ConfigureBtrfs(create, path);
                return;
            }
            if (Statx(_handle, "", 0x1000, DioAlign, out var stat) != 0)
                throw Error("Não foi possível consultar alinhamento de I/O direto", path);
            ValidateAlignment(stat.Mask, stat.MemoryAlignment, stat.OffsetAlignment);
        }
        catch (EntryPointNotFoundException ex)
        {
            _handle.Dispose();
            throw new NotSupportedException("A libc não oferece statx; I/O direto não confirmado.", ex);
        }
        catch { _handle.Dispose(); throw; }
    }

    private void ConfigureBtrfs(bool create, string path)
    {
        if (BtrfsInfo(_handle, 0x8400941F, out var info) != 0)
            throw Error("Não foi possível consultar o setor Btrfs", path);
        ValidateAlignment(DioAlign, info.SectorSize, info.SectorSize);
        const int noCow = 0x00800000, noCompress = 0x400, compress = 4;
        int flags = 0;
        if (FileFlags(_handle, 0x80086601, ref flags) != 0) throw Error("Flags Btrfs indisponíveis", path);
        if (create)
        {
            // Somente arquivo novo, vazio e exclusivo. NoCoW implica NoDataSum no Btrfs;
            // evita fallback de escrita direta para page cache em kernels com checksums.
            flags = (flags & ~compress) | noCow | noCompress;
            if (FileFlags(_handle, 0x40086602, ref flags) != 0) throw Error("Não foi possível preparar arquivo temporário Btrfs", path);
            if (FileFlags(_handle, 0x80086601, ref flags) != 0) throw Error("Não foi possível confirmar flags Btrfs", path);
        }
        if ((flags & (noCow | noCompress)) != (noCow | noCompress) || (flags & compress) != 0)
            throw new NotSupportedException("Arquivo temporário Btrfs sem confirmação NoCoW/NoCompress; medição recusada.");
    }

    public void Dispose() => _handle.Dispose();

    internal static void ValidateAlignment(uint mask, uint memory, uint offset)
    {
        if ((mask & DioAlign) == 0 || memory == 0 || offset == 0 || 4096 % memory != 0 || 4096 % offset != 0)
            throw new NotSupportedException("I/O direto indisponível: o sistema de arquivos/kernel não confirmou alinhamento compatível com blocos de 4 KiB (STATX_DIOALIGN). Escolha outra pasta/sistema de arquivos.");
    }

    public void Read(AlignedBuffer buffer, int count, long offset, CancellationToken ct) => Transfer(buffer, count, offset, false, ct);
    public void Write(AlignedBuffer buffer, int count, long offset, CancellationToken ct) => Transfer(buffer, count, offset, true, ct);
    private void Transfer(AlignedBuffer buffer, int count, long offset, bool write, CancellationToken ct)
    {
        if (buffer.Pointer == 0) throw new ObjectDisposedException(nameof(buffer));
        if (count <= 0 || count > buffer.Length || count % 4096 != 0 || offset < 0 || offset % 4096 != 0)
            throw new ArgumentException("Buffer, tamanho ou offset inválido para I/O direto.");
        nint bytes;
        do
        {
            ct.ThrowIfCancellationRequested();
            bytes = write ? Pwrite(_handle, buffer.Pointer, (nuint)count, offset) : Pread(_handle, buffer.Pointer, (nuint)count, offset);
        } while (bytes == -1 && Marshal.GetLastPInvokeError() == 4); // EINTR
        if (bytes == -1) throw Error(write ? "Falha de escrita direta" : "Falha de leitura direta", "arquivo de benchmark");
        ValidateTransfer((long)bytes, count);
        ct.ThrowIfCancellationRequested();
    }

    internal static void ValidateTransfer(long actual, int expected)
    {
        if (actual != expected) throw new IOException($"I/O incompleto: {actual} de {expected} bytes; medição descartada.");
    }
    internal static void RequirePlatform()
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("O backend de I/O direto desta versão requer Linux x64.");
    }
    internal static IOException Error(string operation, string path)
    {
        int error = Marshal.GetLastPInvokeError();
        return new IOException($"{operation}: {path}. errno {error}: {new Win32Exception(error).Message}");
    }

    [StructLayout(LayoutKind.Explicit, Size = 120)]
    private struct FilesystemData { [FieldOffset(0)] public long Type; }
    [StructLayout(LayoutKind.Explicit, Size = 1024)]
    private struct BtrfsData { [FieldOffset(36)] public uint SectorSize; }
    [DllImport("libc", EntryPoint = "fstatfs", SetLastError = true)]
    private static extern int Fstatfs(SafeFileHandle fd, out FilesystemData data);
    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int BtrfsInfo(SafeFileHandle fd, nuint request, out BtrfsData data);
    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int FileFlags(SafeFileHandle fd, nuint request, ref int flags);

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct StatxData
    {
        [FieldOffset(0)] public uint Mask;
        [FieldOffset(152)] public uint MemoryAlignment;
        [FieldOffset(156)] public uint OffsetAlignment;
    }
    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mode);
    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int Statx(SafeFileHandle fd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mask, out StatxData stat);
    [DllImport("libc", EntryPoint = "pread", SetLastError = true)]
    private static extern nint Pread(SafeFileHandle fd, nint buffer, nuint count, long offset);
    [DllImport("libc", EntryPoint = "pwrite", SetLastError = true)]
    private static extern nint Pwrite(SafeFileHandle fd, nint buffer, nuint count, long offset);
}
