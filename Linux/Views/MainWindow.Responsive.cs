using Avalonia.Controls;

namespace ResetExplode;

public partial class MainWindow
{
    protected override void OnResized(WindowResizedEventArgs e)
    {
        // Some compositors allocate less client space than the requested minimum
        // (for example, after subtracting window decorations in a tiled layout).
        // Reasserting our old minimum makes Avalonia request a resize back on every
        // layout pass. Accept the allocation; never increase the minimum on resize.
        if (e.ClientSize.Width > 0 && e.ClientSize.Width < MinWidth)
            SetCurrentValue(MinWidthProperty, e.ClientSize.Width);
        if (e.ClientSize.Height > 0 && e.ClientSize.Height < MinHeight)
            SetCurrentValue(MinHeightProperty, e.ClientSize.Height);
        base.OnResized(e);
    }
}
