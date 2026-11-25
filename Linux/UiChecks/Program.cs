using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ResetExplode;

// Opt-in graphical regression: requires a desktop, reads telemetry, never starts workloads.
string temporary = Path.Combine(Path.GetTempPath(), "reset-ui-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(temporary, "config"));
Environment.SetEnvironmentVariable("XDG_STATE_HOME", Path.Combine(temporary, "state"));
string? captures = args.Length == 1 ? Path.GetFullPath(args[0]) : null;
if (captures != null) Directory.CreateDirectory(captures);
AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();
var window = new ResizeCheckWindow();
window.CheckMinimumNegotiation();
var root = (Grid)window.Content!;
root.HorizontalAlignment = HorizontalAlignment.Left;
root.VerticalAlignment = VerticalAlignment.Top;
var system = window.FindControl<TabControl>("SystemSections")!;
var bench = window.FindControl<TabControl>("BenchmarkSections")!;
var cases = (from size in new[] { new Size(820, 589), new Size(820, 620), new Size(960, 620), new Size(1180, 850) }
             from page in new[] { 0, 1 }
             from tab in Enumerable.Range(0, page == 0 ? system.ItemCount : bench.ItemCount)
             select (size, page, tab)).ToArray();
using var stop = new CancellationTokenSource();
int current = -1, tick = 0, layouts = 0;
var signatures = new HashSet<string>();
var clientSizes = new HashSet<Size>();
root.LayoutUpdated += (_, _) => layouts++;
string Signature() => string.Join("|", root.GetVisualDescendants().OfType<Control>()
    .Where(c => c.IsEffectivelyVisible && (c is TabControl || c is ScrollViewer))
    .Select(c => $"{c.GetType().Name}:{c.Bounds}"));
var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
void Next()
{
    if (current >= 0)
    {
        if (signatures.Count != 1 || clientSizes.Count != 1 || layouts > 12)
            throw new Exception($"Layout instável: {cases[current]}, {layouts} passes, {signatures.Count} layouts, tamanhos nativos: {string.Join("; ", clientSizes)}.");
        Console.WriteLine($"OK: {cases[current]} · {layouts} passes · layout e janela estáveis.");
    }
    if (++current == cases.Length) { timer.Stop(); window.Close(); return; }
    var c = cases[current];
    // Constrain the content explicitly: window managers may override requested window sizes.
    root.Width = c.size.Width; root.Height = c.size.Height;
    window.FindControl<Button>(c.page == 0 ? "SystemNav" : "BenchNav")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    (c.page == 0 ? system : bench).SelectedIndex = c.tab;
    layouts = 0; signatures.Clear(); clientSizes.Clear(); tick = 0;
}
timer.Tick += (_, _) =>
{
    try
    {
        if (current < 0) { Next(); return; }
        if (++tick >= 4) { signatures.Add(Signature()); clientSizes.Add(window.ClientSize); }
        if (tick == 10)
        {
            if (captures != null && cases[current].size.Width == 820)
            {
                var c = cases[current];
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)c.size.Width, (int)c.size.Height));
                bitmap.Render(root);
                bitmap.Save(Path.Combine(captures, $"layout-{c.size.Width}-{c.size.Height}-{c.page}-{c.tab}.png"));
            }
            Next();
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; timer.Stop(); window.Close();
    }
};
window.Closed += (_, _) => stop.Cancel();
window.Show(); timer.Start(); Dispatcher.UIThread.MainLoop(stop.Token);
Directory.Delete(temporary, recursive: true);

internal sealed class ResizeCheckWindow : MainWindow
{
    public void CheckMinimumNegotiation()
    {
        // Avalonia creates these events internally. Reflection is confined to this
        // regression harness so we can simulate compositor allocations, including zero.
        var constructor = typeof(WindowResizedEventArgs).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null, new[] { typeof(Size), typeof(WindowResizeReason) }, modifiers: null)
            ?? throw new Exception("Construtor do evento de resize mudou; atualize o teste para esta versão do Avalonia.");
        void Resize(double width, double height) => OnResized((WindowResizedEventArgs)constructor.Invoke(
            new object[] { new Size(width, height), WindowResizeReason.Unspecified }));
        double width = MinWidth, height = MinHeight;
        Resize(0, 0);
        if (MinWidth != width || MinHeight != height) throw new Exception("Resize vazio alterou o mínimo.");
        Resize(width - 31, height - 31);
        if (MinWidth != width - 31 || MinHeight != height - 31) throw new Exception("Mínimo recusou a área útil alocada.");
        Resize(width + 100, height + 100);
        if (MinWidth != width - 31 || MinHeight != height - 31) throw new Exception("Resize maior restaurou o mínimo e pode reabrir o ciclo.");
        MinWidth = width; MinHeight = height;
        Console.WriteLine("OK: mínimo acompanha somente alocações positivas menores; crescer não reabre negociação.");
    }
}
