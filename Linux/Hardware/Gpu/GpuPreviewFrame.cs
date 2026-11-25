using System.Runtime.InteropServices;

namespace ResetExplode.Hardware;

/// <summary>Owned snapshot of a completed frame, tightly packed RGBA8, top row first.</summary>
public sealed class GpuPreviewFrame
{
    private readonly byte[] _pixels;
    public int Width { get; }
    public int Height { get; }
    public long FrameNumber { get; }
    public double Seconds { get; }
    public ReadOnlyMemory<byte> Pixels => _pixels;

    public GpuPreviewFrame(int width, int height, long frameNumber, double seconds, ReadOnlySpan<byte> pixels)
    {
        if (width <= 0 || height <= 0 || width > 4096 || height > 4096 || pixels.Length != checked(width * height * 4))
            throw new ArgumentException("Dimensões ou pixels inválidos para a prévia GPU.");
        if (frameNumber < 1 || !double.IsFinite(seconds) || seconds < 0)
            throw new ArgumentException("Identificação inválida do quadro GPU.");
        Width = width; Height = height; FrameNumber = frameNumber; Seconds = seconds;
        _pixels = pixels.ToArray(); // Never expose the mapped Vulkan allocation to the UI.
    }

    public int NonBlackPixels()
    {
        int count = 0;
        for (int i = 0; i < _pixels.Length; i += 4)
            if (_pixels[i] != 0 || _pixels[i + 1] != 0 || _pixels[i + 2] != 0) count++;
        return count;
    }

    /// <summary>The caller owns a framebuffer with at least stride * Height bytes.</summary>
    public void CopyRgbaTo(nint address, int stride)
    {
        if (address == 0 || stride < Width * 4 || (long)stride * Height > int.MaxValue)
            throw new ArgumentException("Framebuffer inválido para a prévia GPU.");
        for (int y = 0; y < Height; y++)
            Marshal.Copy(_pixels, y * Width * 4, address + y * stride, Width * 4);
    }
}

/// <summary>Single producer/consumer slot. A slow UI drops old frames instead of queuing them.</summary>
public sealed class GpuPreviewBuffer
{
    private GpuPreviewFrame? _latest;
    public void Publish(GpuPreviewFrame frame) => Interlocked.Exchange(ref _latest, frame);
    public GpuPreviewFrame? Take() => Interlocked.Exchange(ref _latest, null);
}
