using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ResetExplode;

// Guided desktop acceptance: the operator uses the real keyboard and storage picker.
// No fake storage provider, injected keys, or GPU workload is used.
internal static class GpuInteractionChecks
{
    public static void Run(string directory)
    {
        string output = Path.GetFullPath(directory);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Use um diretório vazio para não confundir evidências anteriores.");
        Directory.CreateDirectory(output);
        string temporary = Directory.CreateTempSubdirectory("reset-gpu-interaction-").FullName;
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(temporary, "config"));
        Environment.SetEnvironmentVariable("XDG_STATE_HOME", Path.Combine(temporary, "state"));
        var fixture = new BenchmarkHistoryEntry
        {
            Id = Guid.NewGuid(), Date = DateTimeOffset.UtcNow, Test = "gpu-stress",
            Name = "FIXTURE de exportação GPU — sem carga real",
            Summary = "Registro sintético para validar o diálogo; não comprova execução GPU.",
            Outcome = BenchmarkOutcome.Cancelled, ThermalEnabled = true, ThermalLimit = 80,
            Context = new() { ["validation"] = "synthetic-dialog-fixture", ["gpuPci"] = "fixture-only" }
        };
        var persistence = new LocalPersistence(XdgPaths.ForCurrentUser());
        persistence.AppendHistory([fixture]);
        var evidence = new List<string>();
        void Log(string message) { evidence.Add(message); Console.WriteLine(message); }
        AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();
        using var done = new CancellationTokenSource();
        Dispatcher.UIThread.Post(async () =>
        {
            MainWindow? window = null;
            bool closed = false;
            try
            {
                window = new MainWindow { Width = 820, Height = 620 };
                window.Closed += (_, _) => closed = true;
                // Block workload buttons in this harness, including keyboard activation.
                string[] workloads = ["SingleButton", "MultiButton", "SimButton", "CpuStressStartButton",
                    "LatencyButton", "BandwidthButton", "RamStressStartButton", "GpuStartButton",
                    "DiskBenchmarkButton", "LoadingBenchmarkButton"];
                foreach (string name in workloads)
                    window.FindControl<Button>(name)!.SetValue(InputElement.IsEnabledProperty, false, BindingPriority.Animation);
                window.Show();
                window.FindControl<Button>("BenchNav")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var tabs = window.FindControl<TabControl>("BenchmarkSections")!;
                tabs.SelectedIndex = 3;
                var selector = window.FindControl<ComboBox>("GpuSelector")!;
                async Task Until(Func<bool> ready, string stage)
                {
                    var deadline = DateTime.UtcNow.AddMinutes(10);
                    while (!ready())
                    {
                        if (closed) throw new OperationCanceledException("Janela fechada durante " + stage);
                        if (DateTime.UtcNow > deadline) throw new TimeoutException(stage);
                        await Task.Delay(100);
                    }
                }
                await Until(() => selector.IsEnabled && selector.ItemCount > 0, "detecção de GPUs");
                Log($"Área nativa inicial: {window.ClientSize}; solicitado: 820×620.");
                bool opened = false, navigation = false, tab = false, shiftTab = false;
                bool leftSelector = false, returnedToSelector = false;
                selector.DropDownOpened += (_, _) => opened = true;
                selector.LostFocus += (_, _) => leftSelector = true;
                selector.GotFocus += (_, _) => { if (leftSelector) returnedToSelector = true; };
                window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
                {
                    if (e.Key == Key.Tab)
                    {
                        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) shiftTab = true;
                        else tab = true;
                    }
                    if (selector.IsKeyboardFocusWithin && e.Key is Key.Down or Key.Up) navigation = true;
                }, RoutingStrategies.Tunnel, handledEventsToo: true);
                selector.BringIntoView();
                selector.Focus();
                window.Title = "VALIDAÇÃO: Alt+↓, escolha GPU, Enter, Tab e Shift+Tab";
                Log("Com foco no seletor: abra com Alt+↓, escolha com setas, confirme com Enter; use Tab e Shift+Tab para sair e voltar. Não inicie stress.");
                await Until(() => opened && navigation && tab && shiftTab && leftSelector && returnedToSelector &&
                    selector.SelectedItem != null && !selector.IsDropDownOpen, "teclado/dropdown/foco");
                Log($"OK: dropdown, setas, seleção e retorno de foco; área nativa: {window.ClientSize}.");
                Log(window.ClientSize == new Size(820, 620)
                    ? "Área nativa mínima 820×620 observada durante interação. Inspeção visual ainda depende do operador."
                    : "PENDENTE: interação no mínimo nativo 820×620; compositor alocou outra área.");
                tabs.SelectedIndex = 0;
                var htmlButton = window.FindControl<Button>("ExportHtmlButton")!;
                var jsonButton = window.FindControl<Button>("ExportJsonButton")!;
                var status = window.FindControl<TextBlock>("ExportStatusText")!;
                htmlButton.BringIntoView();
                window.Title = "VALIDAÇÃO: cancele o diálogo de salvar";
                Log("Cancele o primeiro diálogo de salvar, usando Escape ou Cancelar.");
                htmlButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Until(() => status.Text == "Exportação cancelada." && htmlButton.IsEnabled, "cancelar diálogo");
                if (Directory.EnumerateFileSystemEntries(output).Any()) throw new Exception("Cancelamento criou arquivo no destino de evidências.");
                Log("OK: diálogo cancelado, controles liberados e destino vazio.");
                foreach (string extension in new[] { "html", "json" })
                {
                    string destination = Path.Combine(output, "gpu-dialog." + extension);
                    window.Title = "VALIDAÇÃO: salve em " + destination;
                    Log("No diálogo, salve exatamente em: " + destination);
                    status.Text = "";
                    (extension == "html" ? htmlButton : jsonButton).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Until(() => (status.Text?.StartsWith("Relatório exportado:") == true ||
                        status.Text?.StartsWith("Não foi possível exportar:") == true || status.Text == "Exportação cancelada.") &&
                        htmlButton.IsEnabled && jsonButton.IsEnabled, "salvar " + extension);
                    if (status.Text?.StartsWith("Relatório exportado:") != true) throw new Exception(status.Text);
                    string content = File.ReadAllText(destination);
                    if (extension == "json")
                    {
                        using var json = JsonDocument.Parse(content);
                        var rows = json.RootElement.GetProperty("benchmarks");
                        if (rows.GetArrayLength() != 1) throw new Exception("Número inesperado de registros exportados.");
                        var row = rows[0];
                        var result = row.GetProperty("result");
                        if (!row.GetProperty("savedLocally").GetBoolean() || result.GetProperty("id").GetGuid() != fixture.Id ||
                            result.GetProperty("outcome").GetString() != "cancelled" || result.GetProperty("score").ValueKind != JsonValueKind.Null ||
                            result.GetProperty("metrics").EnumerateObject().Any() ||
                            result.GetProperty("context").GetProperty("validation").GetString() != "synthetic-dialog-fixture")
                            throw new Exception("JSON não preservou a fixture cancelada sem resultados parciais.");
                    }
                    else if (!content.StartsWith("<!doctype html>") || !content.Contains("CANCELADO") ||
                        !content.Contains("synthetic-dialog-fixture") || !content.EndsWith("</body></html>"))
                        throw new Exception("HTML incompleto ou sem a fixture.");
                    Log($"OK: {extension} salvo pelo StorageProvider real e conteúdo conferido.");
                }
                var history = persistence.LoadHistory();
                if (history.Notice != null || history.Value.Entries.Count != 1 || history.Value.Entries[0].Id != fixture.Id)
                    throw new Exception("Histórico isolado foi alterado durante validação.");
                Log("OK: fluxo guiado concluído. Fixture não representa execução real. Inspeção visual e outros tamanhos não são certificados por este teste.");
            }
            catch (Exception ex) { Log("NÃO CONCLUÍDO: " + ex.Message); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "interaction-checks.txt"), evidence);
                window?.Close();
                done.Cancel();
            }
        });
        Dispatcher.UIThread.MainLoop(done.Token);
        Directory.Delete(temporary, true);
    }
}
