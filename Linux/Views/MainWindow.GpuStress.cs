using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Interactivity;
using ResetExplode.Hardware;

namespace ResetExplode;

public partial class MainWindow
{
    private Task _gpuDiscoveryWork = Task.CompletedTask;
    private bool _gpuDiscovering;
    private GpuRenderProgress? _gpuProgress;
    private double _gpuDurationSeconds;
    private readonly GpuPreviewBuffer _gpuPreview = new();
    private WriteableBitmap? _gpuBitmap;
    private long _gpuPreviewFrameNumber;
    private string _gpuPreviewDevice = "";

    private void ResetGpuPreview()
    {
        _gpuPreview.Take();
        GpuPreviewImage.Source = null;
        _gpuBitmap?.Dispose();
        _gpuBitmap = null;
        _gpuPreviewFrameNumber = 0;
        GpuPreviewPlaceholder.IsVisible = true;
        GpuPreviewStatusText.Text = "Inicie o stress para acompanhar a cena gerada pela GPU selecionada.";
    }

    private void RefreshGpuPreview()
    {
        var frame = _gpuPreview.Take();
        if (frame == null || _closeWhenStopped) return;
        if (_gpuBitmap == null)
        {
            _gpuBitmap = new WriteableBitmap(new PixelSize(frame.Width, frame.Height), new Vector(96, 96),
                PixelFormat.Rgba8888, AlphaFormat.Opaque);
            GpuPreviewImage.Source = _gpuBitmap;
        }
        using (var framebuffer = _gpuBitmap.Lock()) frame.CopyRgbaTo(framebuffer.Address, framebuffer.RowBytes);
        _gpuPreviewFrameNumber = frame.FrameNumber;
        GpuPreviewPlaceholder.IsVisible = false;
        GpuPreviewImage.InvalidateVisual();
        GpuPreviewStatusText.Text = $"{_gpuPreviewDevice} · quadro {frame.FrameNumber} · {frame.Seconds:N1} s de teste";
    }


    private async void RefreshGpuDevices(object? sender, RoutedEventArgs e) => await StartGpuDiscovery();

    private Task StartGpuDiscovery()
    {
        if (_gpuDiscovering || _closeWhenStopped || _cancellation != null) return _gpuDiscoveryWork;
        _gpuDiscoveryWork = DiscoverGpuDevicesAsync();
        return _gpuDiscoveryWork;
    }

    private async Task DiscoverGpuDevicesAsync()
    {
        string? selectedPci = (GpuSelector.SelectedItem as GpuStressChoice)?.Device.PciAddress;
        _gpuDiscovering = true;
        GpuSelector.ItemsSource = null;
        GpuDetectionText.Text = "Detectando GPUs e verificando sensores…";
        GpuSelectionText.Text = "A detecção não inicia carga gráfica.";
        UpdateGpuControls();
        try
        {
            var inventory = await Task.Run(LinuxGpuInventory.Read);
            if (_closeWhenStopped) return;
            GpuInventoryEntries.ItemsSource = inventory;
            GpuInventoryStatus.Text = inventory.Count == 0 ? "Nenhuma GPU PCI identificada. Verifique o acesso ao sysfs."
                : "Dados reportados pelo driver. Link atual pode variar com economia de energia. Memória de integrada não é estimada pela RAM do sistema.";
            var choices = await Task.Run(GpuStressDiscovery.Collect);
            if (_closeWhenStopped) return;
            GpuInventoryEntries.ItemsSource = LinuxGpuInventory.WithVulkan(inventory, choices.Select(c => c.Device).ToArray());
            GpuSelector.ItemsSource = choices;
            GpuSelector.SelectedItem = GpuStressDiscovery.RestoreSelection(choices, selectedPci);
            GpuDetectionText.Text = choices.Count == 0 ? "Nenhuma GPU Vulkan encontrada. Verifique o driver e atualize a lista."
                : $"{choices.Count} dispositivo(s) detectado(s). Escolha a GPU que deseja testar.";
        }
        catch (Exception ex)
        {
            if (!_closeWhenStopped) GpuDetectionText.Text = $"Detecção indisponível: {ex.Message} Verifique o driver Vulkan e tente atualizar.";
        }
        finally
        {
            _gpuDiscovering = false;
            UpdateGpuSelection();
        }
    }

    private void GpuSelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateGpuSelection();
    private void UpdateGpuSelection()
    {
        if (GpuSelectionText == null) return; // Eventos durante InitializeComponent.
        GpuSelectionText.Text = (GpuSelector.SelectedItem as GpuStressChoice)?.Status ?? "Selecione uma GPU. Nenhum dispositivo é escolhido automaticamente.";
        if (_cancellation == null && GpuPreviewImage != null) ResetGpuPreview();
        UpdateGpuControls();
    }

    private void UpdateGpuControls()
    {
        if (GpuStartButton == null) return;
        bool available = _cancellation == null && !_gpuDiscovering && !_closeWhenStopped && !_exporting &&
            !_cleaningMemory && !_clearingHistory && _latencyCancellation == null && _throughputCancellation == null && _dnsCancellation == null;
        GpuSelector.IsEnabled = GpuRefreshButton.IsEnabled = GpuMinutes.IsEnabled = GpuLimit.IsEnabled = available;
        GpuStartButton.IsEnabled = available && GpuSelector.SelectedItem is GpuStressChoice { CanStress: true };
    }

    private async void RunGpuStress(object? sender, RoutedEventArgs e)
    {
        if (!GpuStartButton.IsEnabled || GpuSelector.SelectedItem is not GpuStressChoice { CanStress: true } choice || choice.Device.PciAddress == null) return;
        string pci = choice.Device.PciAddress;
        double limit = (double)(GpuLimit.Value ?? 80);
        var duration = TimeSpan.FromMinutes((double)(GpuMinutes.Value ?? 1));
        _gpuDurationSeconds = duration.TotalSeconds;
        ResetGpuPreview();
        _gpuPreviewDevice = choice.Device.Name;
        GpuPreviewStatusText.Text = "Preparando a GPU; aguardando o primeiro quadro…";
        GpuPreviewPanel.BringIntoView();
        GpuResultText.Text = $"Preparando {choice.Device.Name}; verificando novamente GPU e temperatura…";
        try
        {
            await RunAsync("gpu-stress", $"Stress GPU · {choice.Device.Name}", ct =>
            {
                var result = VulkanGpuRenderer.Run(pci, duration, limit, ct, p => Volatile.Write(ref _gpuProgress, p), choice.Device, _gpuPreview.Publish);
                return new BenchmarkMeasurement($"{result.Device} · {result.Frames} frames em {result.Seconds:N1} s. Não comprova estabilidade.",
                    null, new() { ["frames"] = result.Frames, ["seconds"] = result.Seconds, ["nonBlackPixels"] = result.NonBlackPixels });
            }, new() { ["gpuName"] = choice.Device.Name, ["gpuType"] = choice.Device.Kind, ["gpuPci"] = pci,
                ["durationSeconds"] = duration.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["duration"] = "render loop includes periodic preview readback; excludes initialization and final readback",
                ["preview"] = "RGBA8 640x360 readback; at most 5 updates/s plus final frame; latest frame only",
                ["methodology"] = "linux-gpu-stress-preview-v2" }, gpuThermalLimit: limit);
        }
        finally
        {
            Volatile.Write(ref _gpuProgress, null);
            RefreshGpuPreview();
            if (!_closeWhenStopped)
                GpuPreviewStatusText.Text = _gpuPreviewFrameNumber == 0
                    ? "Teste encerrado sem imagem recebida. Consulte o resultado abaixo."
                    : $"Imagem parada · {_gpuPreviewDevice} · último quadro recebido: {_gpuPreviewFrameNumber}. Consulte o resultado abaixo.";
        }
    }
}
