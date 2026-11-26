using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ResetExplode;
using ResetExplode.Hardware;

// Opt-in: starts real GPU workloads, closes during preview, reopens with the same isolated history.
internal static class GpuLifecycleChecks
{
    public static void Run(string pci, string output)
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        string temporary = Directory.CreateTempSubdirectory("reset-gpu-lifecycle-").FullName;
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(temporary, "config"));
        Environment.SetEnvironmentVariable("XDG_STATE_HOME", Path.Combine(temporary, "state"));
        AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();
        using var done = new CancellationTokenSource();
        Dispatcher.UIThread.Post(async () =>
        {
            MainWindow? window = null;
            try
            {
                for (int run = 0; run < 2; run++)
                {
                    window = new MainWindow { Width = 820, Height = 620 };
                    var closed = new TaskCompletionSource();
                    window.Closed += (_, _) => closed.TrySetResult();
                    window.Show();
                    Click(window, "BenchNav");
                    window.FindControl<TabControl>("BenchmarkSections")!.SelectedIndex = 3;
                    var selector = window.FindControl<ComboBox>("GpuSelector")!;
                    await Until(() => selector.IsEnabled && selector.ItemCount > 0, "detecção de GPU");
                    var choice = selector.Items.OfType<GpuStressChoice>().Single(c => c.Device.PciAddress == pci);
                    if (!choice.CanStress) throw new Exception(choice.Status);
                    selector.SelectedItem = choice;
                    if (!window.FindControl<Button>("GpuStartButton")!.IsEnabled) throw new Exception("Início permaneceu bloqueado.");
                    window.FindControl<NumericUpDown>("GpuLimit")!.Value = 80;
                    Click(window, "GpuStartButton");
                    await Until(() => window.FindControl<Image>("GpuPreviewImage")!.Source != null, "primeiro quadro");
                    if (!window.FindControl<Button>("CancelButton")!.IsEnabled || selector.IsEnabled)
                        throw new Exception("Controles inconsistentes durante carga.");
                    using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height)))
                    {
                        bitmap.Render(window);
                        bitmap.Save(Path.Combine(output, $"gpu-lifecycle-{run}.png"));
                    }
                    Console.WriteLine($"Execução {run + 1}: prévia ativa; tamanho nativo {window.ClientSize}.");
                    if (run == 1)
                    {
                        Click(window, "CancelButton");
                        await Until(() => window.FindControl<Button>("GpuStartButton")!.IsEnabled, "cancelamento e liberação");
                    }
                    window.Close();
                    await closed.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    window = null;
                    var history = new LocalPersistence(XdgPaths.ForCurrentUser()).LoadHistory();
                    if (history.Notice != null || history.Value.Entries.Count != run + 1 ||
                        history.Value.Entries.Any(e => e.Test != "gpu-stress" || e.Outcome != BenchmarkOutcome.Cancelled ||
                            e.Metrics.Count != 0 || e.Score != null || e.Context.GetValueOrDefault("gpuPci") != pci))
                        throw new Exception("Histórico não preservou cancelamento/PCI ou publicou métricas parciais.");
                    var report = BenchmarkReportBuilder.Capture(history.Value.Entries, [], "ui-check");
                    File.WriteAllText(Path.Combine(output, "gpu-lifecycle.json"), BenchmarkReportBuilder.Json(report));
                    File.WriteAllText(Path.Combine(output, "gpu-lifecycle.html"), BenchmarkReportBuilder.Html(report));
                    Console.WriteLine($"OK: execução {run + 1}, encerramento, persistência e relatórios sem métricas parciais.");
                }
                Console.WriteLine("OK: reabertura após fechar sob carga permitiu novo stress e cancelamento. Exportação pelo diálogo e teclado nativo não são cobertos por este teste.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally
            {
                // Never terminate a live renderer from the harness: request the application's normal shutdown.
                if (window != null)
                {
                    var closed = new TaskCompletionSource();
                    window.Closed += (_, _) => closed.TrySetResult();
                    window.Close();
                    await closed.Task;
                }
                done.Cancel();
            }
        });
        Dispatcher.UIThread.MainLoop(done.Token);
        Directory.Delete(temporary, true);
    }

    private static void Click(MainWindow window, string name) =>
        window.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task Until(Func<bool> ready, string stage)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!ready())
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException(stage);
            await Task.Delay(100);
        }
    }
}
