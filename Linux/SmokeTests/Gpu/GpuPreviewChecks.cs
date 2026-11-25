using System.Runtime.InteropServices;
using ResetExplode.Hardware;

internal static class GpuPreviewChecks
{
    public static void Run()
    {
        byte[] source = [255, 0, 0, 255, 0, 0, 0, 255, 0, 0, 255, 255, 0, 255, 0, 255];
        var frame = new GpuPreviewFrame(2, 2, 1, 0.5, source);
        source[0] = 0;
        if (frame.Pixels.Span[0] != 255 || frame.NonBlackPixels() != 3) throw new Exception("Quadro não possui seus pixels ou conta alfa como cor.");
        var memory = Marshal.AllocHGlobal(24);
        try
        {
            Marshal.Copy(Enumerable.Repeat((byte)77, 24).ToArray(), 0, memory, 24);
            frame.CopyRgbaTo(memory, 12);
            var copied = new byte[24]; Marshal.Copy(memory, copied, 0, 24);
            if (!copied.AsSpan(0, 8).SequenceEqual(frame.Pixels.Span[..8]) ||
                !copied.AsSpan(12, 8).SequenceEqual(frame.Pixels.Span[8..]) ||
                copied[8] != 77 || copied[11] != 77 || copied[20] != 77 || copied[23] != 77)
                throw new Exception("Prévia alterou canais, orientação ou padding do framebuffer.");
        }
        finally { Marshal.FreeHGlobal(memory); }
        void Refuse(Action action)
        {
            try { action(); } catch (ArgumentException) { return; }
            throw new Exception("Prévia aceitou entrada inválida.");
        }
        Refuse(() => new GpuPreviewFrame(2, 2, 1, 0, new byte[15]));
        Refuse(() => new GpuPreviewFrame(0, 2, 1, 0, []));
        Refuse(() => new GpuPreviewFrame(int.MaxValue, 2, 1, 0, []));
        Refuse(() => new GpuPreviewFrame(2, 2, 0, 0, source));
        Refuse(() => new GpuPreviewFrame(2, 2, 1, double.NaN, source));
        Refuse(() => frame.CopyRgbaTo(0, 8));
        Refuse(() => frame.CopyRgbaTo(1, 7));
        var buffer = new GpuPreviewBuffer();
        buffer.Publish(frame);
        var second = new GpuPreviewFrame(2, 2, 2, 0.8, source);
        buffer.Publish(second);
        if (buffer.Take() != second || buffer.Take() != null) throw new Exception("Prévia acumulou quadros ou repetiu consumo.");
        Parallel.For(0, 500, i => { buffer.Publish(i % 2 == 0 ? frame : second); buffer.Take(); });
        buffer.Take(); buffer.Publish(frame);
        if (buffer.Take() != frame || buffer.Take() != null) throw new Exception("Slot de prévia inválido após concorrência.");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        int calls = 0;
        try { VulkanGpuRenderer.Run("0000:01:00.0", TimeSpan.FromSeconds(1), cancellation: cancel.Token, preview: _ => calls++); throw new Exception("Pré-cancelamento ignorado."); }
        catch (OperationCanceledException) { }
        if (calls != 0) throw new Exception("Prévia publicada após pré-cancelamento.");
        Console.WriteLine("OK: prévia GPU RGBA, propriedade dos pixels, stride, padding, limites, slot único e pré-cancelamento.");
    }
}
