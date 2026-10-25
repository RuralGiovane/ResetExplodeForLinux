using System.Text;
using ResetExplode.Hardware;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace ResetExplode;

public partial class MainWindow
{
    private enum ExportKind { Benchmarks, Storage, Power, Serial, Network }
    private bool _exporting;
    private Task _exportWork = Task.CompletedTask;

    private async void ExportHtml(object? sender, RoutedEventArgs e) => await StartExportAsync(true);
    private async void ExportJson(object? sender, RoutedEventArgs e) => await StartExportAsync(false);

    private async void ExportStorageHtml(object? sender, RoutedEventArgs e) => await StartExportAsync(true, kind: ExportKind.Storage);
    private async void ExportStorageJson(object? sender, RoutedEventArgs e) => await StartExportAsync(false, kind: ExportKind.Storage);

    private async void ExportPowerHtml(object? sender, RoutedEventArgs e) => await StartExportAsync(true, ExportKind.Power);
    private async void ExportPowerJson(object? sender, RoutedEventArgs e) => await StartExportAsync(false, ExportKind.Power);

    private async void ExportSerialHtml(object? sender, RoutedEventArgs e) => await StartExportAsync(true, ExportKind.Serial);
    private async void ExportSerialJson(object? sender, RoutedEventArgs e) => await StartExportAsync(false, ExportKind.Serial);

    private async void ExportNetworkHtml(object? sender, RoutedEventArgs e) => await StartExportAsync(true, ExportKind.Network);
    private async void ExportNetworkJson(object? sender, RoutedEventArgs e) => await StartExportAsync(false, ExportKind.Network);

    private Task StartExportAsync(bool html, ExportKind kind = ExportKind.Benchmarks)
    {
        if ((_latencyCancellation != null || _throughputCancellation != null || _dnsCancellation != null) || _exporting || _cleaningMemory || _clearingHistory || _cancellation != null || _closeWhenStopped) return Task.CompletedTask;
        return _exportWork = ExportReportAsync(html, kind);
    }

    private async Task ExportReportAsync(bool html, ExportKind kind)
    {
        var status = kind switch
        {
            ExportKind.Storage => StorageExportStatusText,
            ExportKind.Power => PowerExportStatusText,
            ExportKind.Serial => SerialExportStatusText,
            ExportKind.Network => NetworkExportStatusText,
            _ => ExportStatusText
        };
        _exporting = true;
        SetRunning(false);
        try
        {
            if (!StorageProvider.CanSave)
            {
                status.Text = "O ambiente gráfico não disponibiliza o diálogo de salvar arquivos.";
                return;
            }
            await _persistenceWork;
            if (_closeWhenStopped) return;
            string version = typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "0.16";
            string content;
            if (kind == ExportKind.Storage)
            {
                // Captura na UI: consultas em andamento não alteram este relatório depois.
                var report = StorageReportBuilder.Capture(version, _storageInventory, _mountInventory,
                    _inventoryCollectedAt, _lastSmartReport, _smartCollectedAt);
                content = await Task.Run(() => html ? StorageReportBuilder.Html(report) : StorageReportBuilder.Json(report));
            }
            else if (kind == ExportKind.Power)
            {
                var report = PowerReportBuilder.Capture(version, _powerInventory, _powerCollectionError);
                content = await Task.Run(() => html ? PowerReportBuilder.Html(report) : PowerReportBuilder.Json(report));
            }
            else if (kind == ExportKind.Serial)
            {
                var report = SerialReportBuilder.Capture(version, _serialInventory, _serialCollectionError);
                content = await Task.Run(() => html ? SerialReportBuilder.Html(report) : SerialReportBuilder.Json(report));
            }
            else if (kind == ExportKind.Network)
            {
                var report = NetworkReportBuilder.Capture(version, _networkInventory, _networkCollectionError,
                    _networkLatencyResult, _networkLatencyCompletedAt, _networkLatencyStatus,
                    _networkThroughputResult, _networkThroughputStatus, _networkDnsResult, _networkDnsStatus);
                content = await Task.Run(() => html ? NetworkReportBuilder.Html(report) : NetworkReportBuilder.Json(report));
            }
            else
            {
                var report = BenchmarkReportBuilder.Capture(_history, _pendingHistory, version);
                content = await Task.Run(() => html ? BenchmarkReportBuilder.Html(report) : BenchmarkReportBuilder.Json(report));
            }
            string category = kind switch { ExportKind.Storage => "armazenamento", ExportKind.Power => "alimentacao", ExportKind.Serial => "maker", ExportKind.Network => "rede", _ => "benchmarks" };
            string extension = html ? "html" : "json";
            using var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = $"Exportar relatório de {category}",
                SuggestedFileName = $"ResetExplode-{category}-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}",
                DefaultExtension = extension,
                FileTypeChoices = new[] { new FilePickerFileType(html ? "Relatório HTML" : "Dados JSON") { Patterns = new[] { "*." + extension } } }
            });
            if (file == null) { status.Text = "Exportação cancelada."; return; }
            if (_closeWhenStopped) return;
            await using (var stream = await file.OpenWriteAsync())
            {
                // Alguns provedores abrem um arquivo existente sem truncá-lo.
                if (stream.CanSeek) { stream.Position = 0; stream.SetLength(0); }
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                await stream.WriteAsync(bytes);
                await stream.FlushAsync();
            }
            status.Text = $"Relatório exportado: {file.Name}.";
        }
        catch (Exception ex)
        {
            status.Text = $"Não foi possível exportar: {ex.Message}. O arquivo de destino pode estar incompleto.";
        }
        finally { _exporting = false; SetRunning(false); }
    }
}
