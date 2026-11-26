using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace ResetExplode.Hardware;

public sealed record RenderGpu(string Name, string? PciAddress, uint VendorId, bool Hardware)
{
    public string Kind { get; init; } = "Tipo não informado";
    public string? ApiVersion { get; init; }
}
public sealed record GpuRenderProgress(long Frames, double Seconds, double Temperature);
public sealed record GpuRenderResult(string Device, string PciAddress, long Frames, double Seconds, int NonBlackPixels);

/// <summary>Pipeline gráfico Vulkan 1.1 offscreen, uso serial. Uma submissão por vez.</summary>
public sealed unsafe class VulkanGpuRenderer : IDisposable
{
    private readonly Vk _vk;
    private Instance _instance;
    private Device _device;
    private PhysicalDevice _physical;
    private Queue _queue;
    private Image _image;
    private DeviceMemory _memory;
    private ImageView _view;
    private RenderPass _pass;
    private Framebuffer _framebuffer;
    private PipelineLayout _layout;
    private Pipeline _pipeline;
    private CommandPool _pool;
    private CommandBuffer _command;
    private Fence _fence;
    private VkBuffer _readbackBuffer;
    private DeviceMemory _readbackMemory;
    private bool _disposed;
    private readonly uint _width, _height, _shells;
    public RenderGpu Selected { get; private set; } = null!;

    private static void Check(Result result)
    {
        if (result != Result.Success) throw new InvalidOperationException($"Vulkan: {result}.");
    }

    private VulkanGpuRenderer()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Backend disponível apenas no Linux.");
        _vk = Vk.GetApi();
        try
        {
            ApplicationInfo app = new() { SType = StructureType.ApplicationInfo, ApiVersion = Vk.Version11 };
            InstanceCreateInfo info = new() { SType = StructureType.InstanceCreateInfo, PApplicationInfo = &app };
            Check(_vk.CreateInstance(in info, null, out _instance));
        }
        catch { _vk.Dispose(); throw; }
    }

    public static IReadOnlyList<RenderGpu> Enumerate()
    {
        using var renderer = new VulkanGpuRenderer();
        return renderer.Devices().Select(x => x.Info).ToArray();
    }

    private List<(PhysicalDevice Device, RenderGpu Info)> Devices()
    {
        uint count = 0;
        Check(_vk.EnumeratePhysicalDevices(_instance, ref count, null));
        var devices = new PhysicalDevice[count];
        fixed (PhysicalDevice* data = devices) Check(_vk.EnumeratePhysicalDevices(_instance, ref count, data));
        var result = new List<(PhysicalDevice, RenderGpu)>();
        foreach (var device in devices.Take((int)count))
        {
            _vk.GetPhysicalDeviceProperties(device, out var basic);
            string name = Marshal.PtrToStringUTF8((nint)basic.DeviceName) ?? "GPU sem nome";
            uint extCount = 0;
            Check(_vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extCount, null));
            var extensions = new ExtensionProperties[extCount];
            fixed (ExtensionProperties* data = extensions)
                Check(_vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extCount, data));
            bool pciSupported = false;
            foreach (var extension in extensions)
            {
                var copy = extension;
                if (Marshal.PtrToStringUTF8((nint)copy.ExtensionName) == "VK_EXT_pci_bus_info") pciSupported = true;
            }
            string? address = null;
            if (pciSupported && basic.ApiVersion >= Vk.Version11)
            {
                PhysicalDevicePCIBusInfoPropertiesEXT pci = new() { SType = StructureType.PhysicalDevicePciBusInfoPropertiesExt };
                PhysicalDeviceProperties2 properties = new() { SType = StructureType.PhysicalDeviceProperties2, PNext = &pci };
                _vk.GetPhysicalDeviceProperties2(device, &properties);
                if (pci.PciDomain <= 65535 && pci.PciBus <= 255 && pci.PciDevice <= 31 && pci.PciFunction <= 7)
                    address = $"{pci.PciDomain:x4}:{pci.PciBus:x2}:{pci.PciDevice:x2}.{pci.PciFunction:x1}";
            }
            result.Add((device, new(name, address, basic.VendorID,
                basic.DeviceType is PhysicalDeviceType.DiscreteGpu or PhysicalDeviceType.IntegratedGpu)
                { ApiVersion = $"{(basic.ApiVersion >> 22) & 0x7f}.{(basic.ApiVersion >> 12) & 0x3ff}.{basic.ApiVersion & 0xfff}",
                    Kind = basic.DeviceType switch { PhysicalDeviceType.IntegratedGpu => "Integrada", PhysicalDeviceType.DiscreteGpu => "Dedicada",
                    PhysicalDeviceType.Cpu => "Software", PhysicalDeviceType.VirtualGpu => "Virtual", _ => "Outro" } }));
        }
        return result;
    }

    private VulkanGpuRenderer(string pciAddress, uint width, uint height, uint shells) : this()
    {
        _width = width; _height = height; _shells = shells;
        try
        {
            var matches = Devices().Where(x => x.Info.PciAddress == pciAddress && x.Info.Hardware).ToArray();
            if (matches.Length != 1) throw new NotSupportedException("GPU PCI ausente, ambígua ou sem identificação física Vulkan.");
            (_physical, Selected) = matches[0];
            _vk.GetPhysicalDeviceProperties(_physical, out var properties);
            if (properties.ApiVersion < Vk.Version11) throw new NotSupportedException("Vulkan 1.1 é obrigatório.");
            uint count = 0;
            _vk.GetPhysicalDeviceQueueFamilyProperties(_physical, ref count, null);
            var families = new QueueFamilyProperties[count];
            fixed (QueueFamilyProperties* data = families) _vk.GetPhysicalDeviceQueueFamilyProperties(_physical, ref count, data);
            int family = Array.FindIndex(families, f => f.QueueCount > 0 && (f.QueueFlags & QueueFlags.GraphicsBit) != 0);
            if (family < 0) throw new NotSupportedException("Fila gráfica indisponível.");
            float priority = 1;
            DeviceQueueCreateInfo queue = new() { SType = StructureType.DeviceQueueCreateInfo, QueueFamilyIndex = (uint)family, QueueCount = 1, PQueuePriorities = &priority };
            DeviceCreateInfo deviceInfo = new() { SType = StructureType.DeviceCreateInfo, QueueCreateInfoCount = 1, PQueueCreateInfos = &queue };
            Check(_vk.CreateDevice(_physical, in deviceInfo, null, out _device));
            _vk.GetDeviceQueue(_device, (uint)family, 0, out _queue);
            CreateTarget();
            CreatePipeline();
            CommandPoolCreateInfo pool = new() { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = (uint)family, Flags = CommandPoolCreateFlags.ResetCommandBufferBit };
            Check(_vk.CreateCommandPool(_device, in pool, null, out _pool));
            CommandBufferAllocateInfo allocate = new() { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _pool, Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };
            Check(_vk.AllocateCommandBuffers(_device, in allocate, out _command));
            FenceCreateInfo fence = new() { SType = StructureType.FenceCreateInfo };
            Check(_vk.CreateFence(_device, in fence, null, out _fence));
        }
        catch { Dispose(); throw; }
    }

    private void CreateTarget()
    {
        _vk.GetPhysicalDeviceFormatProperties(_physical, Format.R8G8B8A8Unorm, out var format);
        var required = FormatFeatureFlags.ColorAttachmentBit | FormatFeatureFlags.ColorAttachmentBlendBit | FormatFeatureFlags.TransferSrcBit;
        if ((format.OptimalTilingFeatures & required) != required)
            throw new NotSupportedException("Formato RGBA8 não suporta render target.");
        ImageCreateInfo image = new() { SType = StructureType.ImageCreateInfo, ImageType = ImageType.Type2D,
            Format = Format.R8G8B8A8Unorm, Extent = new(_width, _height, 1), MipLevels = 1, ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit, Tiling = ImageTiling.Optimal, Usage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit,
            SharingMode = SharingMode.Exclusive, InitialLayout = ImageLayout.Undefined };
        Check(_vk.CreateImage(_device, in image, null, out _image));
        _vk.GetImageMemoryRequirements(_device, _image, out var requirements);
        _vk.GetPhysicalDeviceMemoryProperties(_physical, out var memory);
        uint? type = null;
        for (uint i = 0; i < memory.MemoryTypeCount; i++)
            if ((requirements.MemoryTypeBits & (1u << (int)i)) != 0 && (memory.MemoryTypes[(int)i].PropertyFlags & MemoryPropertyFlags.DeviceLocalBit) != 0) { type = i; break; }
        if (type == null) throw new NotSupportedException("Memória local para render target indisponível.");
        MemoryAllocateInfo allocation = new() { SType = StructureType.MemoryAllocateInfo, AllocationSize = requirements.Size, MemoryTypeIndex = type.Value };
        Check(_vk.AllocateMemory(_device, in allocation, null, out _memory));
        Check(_vk.BindImageMemory(_device, _image, _memory, 0));
        ImageViewCreateInfo view = new() { SType = StructureType.ImageViewCreateInfo, Image = _image,
            ViewType = ImageViewType.Type2D, Format = image.Format, SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1) };
        Check(_vk.CreateImageView(_device, in view, null, out _view));
        AttachmentDescription attachment = new() { Format = image.Format, Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear, StoreOp = AttachmentStoreOp.Store, StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare, InitialLayout = ImageLayout.Undefined, FinalLayout = ImageLayout.ColorAttachmentOptimal };
        AttachmentReference reference = new(0, ImageLayout.ColorAttachmentOptimal);
        SubpassDescription subpass = new() { PipelineBindPoint = PipelineBindPoint.Graphics, ColorAttachmentCount = 1, PColorAttachments = &reference };
        SubpassDependency dependency = new() { SrcSubpass = Vk.SubpassExternal, DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit, DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit, DstAccessMask = AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit };
        RenderPassCreateInfo pass = new() { SType = StructureType.RenderPassCreateInfo, AttachmentCount = 1, PAttachments = &attachment,
            SubpassCount = 1, PSubpasses = &subpass, DependencyCount = 1, PDependencies = &dependency };
        Check(_vk.CreateRenderPass(_device, in pass, null, out _pass));
        var target = _view;
        FramebufferCreateInfo framebuffer = new() { SType = StructureType.FramebufferCreateInfo, RenderPass = _pass,
            AttachmentCount = 1, PAttachments = &target, Width = _width, Height = _height, Layers = 1 };
        Check(_vk.CreateFramebuffer(_device, in framebuffer, null, out _framebuffer));
    }

    private ShaderModule Shader(string suffix)
    {
        var assembly = typeof(VulkanGpuRenderer).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith(suffix, StringComparison.Ordinal)))!;
        byte[] bytes = new byte[stream.Length]; stream.ReadExactly(bytes);
        fixed (byte* code = bytes)
        {
            ShaderModuleCreateInfo info = new() { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)bytes.Length, PCode = (uint*)code };
            Check(_vk.CreateShaderModule(_device, in info, null, out var shader));
            return shader;
        }
    }

    private void CreatePipeline()
    {
        ShaderModule vertex = default, fragment = default;
        try
        {
            vertex = Shader("burn.vert.spv"); fragment = Shader("burn.frag.spv");
            byte* entry = stackalloc byte[] { 109, 97, 105, 110, 0 };
            var stages = stackalloc PipelineShaderStageCreateInfo[2];
            stages[0] = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.VertexBit, Module = vertex, PName = entry };
            stages[1] = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.FragmentBit, Module = fragment, PName = entry };
            PipelineVertexInputStateCreateInfo input = new() { SType = StructureType.PipelineVertexInputStateCreateInfo };
            PipelineInputAssemblyStateCreateInfo assembly = new() { SType = StructureType.PipelineInputAssemblyStateCreateInfo, Topology = PrimitiveTopology.TriangleList };
            Viewport viewport = new(0, 0, _width, _height, 0, 1);
            Rect2D scissor = new(new(0, 0), new(_width, _height));
            PipelineViewportStateCreateInfo view = new() { SType = StructureType.PipelineViewportStateCreateInfo, ViewportCount = 1, PViewports = &viewport, ScissorCount = 1, PScissors = &scissor };
            PipelineRasterizationStateCreateInfo raster = new() { SType = StructureType.PipelineRasterizationStateCreateInfo, PolygonMode = PolygonMode.Fill, CullMode = CullModeFlags.None, FrontFace = FrontFace.CounterClockwise, LineWidth = 1 };
            PipelineMultisampleStateCreateInfo samples = new() { SType = StructureType.PipelineMultisampleStateCreateInfo, RasterizationSamples = SampleCountFlags.Count1Bit };
            PipelineColorBlendAttachmentState blend = new() { BlendEnable = true, SrcColorBlendFactor = BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha, ColorBlendOp = BlendOp.Add, SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.Zero, AlphaBlendOp = BlendOp.Add,
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit };
            PipelineColorBlendStateCreateInfo blending = new() { SType = StructureType.PipelineColorBlendStateCreateInfo, AttachmentCount = 1, PAttachments = &blend };
            PushConstantRange range = new(ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0, 16);
            PipelineLayoutCreateInfo layout = new() { SType = StructureType.PipelineLayoutCreateInfo, PushConstantRangeCount = 1, PPushConstantRanges = &range };
            Check(_vk.CreatePipelineLayout(_device, in layout, null, out _layout));
            GraphicsPipelineCreateInfo pipeline = new() { SType = StructureType.GraphicsPipelineCreateInfo, StageCount = 2, PStages = stages,
                PVertexInputState = &input, PInputAssemblyState = &assembly, PViewportState = &view, PRasterizationState = &raster,
                PMultisampleState = &samples, PColorBlendState = &blending, Layout = _layout, RenderPass = _pass, Subpass = 0 };
            Check(_vk.CreateGraphicsPipelines(_device, default, 1, in pipeline, null, out _pipeline));
        }
        finally
        {
            if (fragment.Handle != 0) _vk.DestroyShaderModule(_device, fragment, null);
            if (vertex.Handle != 0) _vk.DestroyShaderModule(_device, vertex, null);
        }
    }

    private void Draw(float seconds, Action observe, Action submitted)
    {
        Check(_vk.ResetCommandBuffer(_command, 0));
        CommandBufferBeginInfo begin = new() { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
        Check(_vk.BeginCommandBuffer(_command, in begin));
        ClearValue clear = new() { Color = new ClearColorValue(0, 0, 0, 1) };
        RenderPassBeginInfo pass = new() { SType = StructureType.RenderPassBeginInfo, RenderPass = _pass, Framebuffer = _framebuffer,
            RenderArea = new(new(0, 0), new(_width, _height)), ClearValueCount = 1, PClearValues = &clear };
        _vk.CmdBeginRenderPass(_command, in pass, SubpassContents.Inline);
        _vk.CmdBindPipeline(_command, PipelineBindPoint.Graphics, _pipeline);
        float* constants = stackalloc float[] { seconds, (float)_width / _height, _shells, 32 };
        _vk.CmdPushConstants(_command, _layout, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0, 16, constants);
        _vk.CmdDraw(_command, 160 * 64 * 6, _shells, 0, 0);
        _vk.CmdEndRenderPass(_command);
        Check(_vk.EndCommandBuffer(_command));
        Check(_vk.ResetFences(_device, 1, in _fence));
        var command = _command;
        SubmitInfo submit = new() { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &command };
        observe(); // Checagem imediatamente antes de submeter trabalho.
        Check(_vk.QueueSubmit(_queue, 1, in submit, _fence));
        submitted();
        WaitForFrame(observe);
    }

    private void WaitForFrame(Action observe)
    {
        Exception? stopped = null;
        // Não liberar recursos em uso pela GPU. Cancelamento impede novas submissões,
        // mas ainda espera a fence do frame já enviado (driver travado pode atrasar o retorno).
        while (true)
        {
            var result = _vk.WaitForFences(_device, 1, in _fence, true, 100_000_000);
            if (stopped == null) { try { observe(); } catch (Exception ex) { stopped = ex; } }
            if (result == Result.Timeout) continue;
            Check(result); break;
        }
        if (stopped != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(stopped).Throw();
    }

    private void EnsureReadbackBuffer()
    {
        if (_readbackBuffer.Handle != 0) return;
        ulong size = (ulong)_width * _height * 4;
        BufferCreateInfo bufferInfo = new() { SType = StructureType.BufferCreateInfo, Size = size,
            Usage = BufferUsageFlags.TransferDstBit, SharingMode = SharingMode.Exclusive };
        Check(_vk.CreateBuffer(_device, in bufferInfo, null, out _readbackBuffer));
        _vk.GetBufferMemoryRequirements(_device, _readbackBuffer, out var requirements);
        _vk.GetPhysicalDeviceMemoryProperties(_physical, out var properties);
        uint? type = null;
        var flags = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit;
        for (uint i = 0; i < properties.MemoryTypeCount; i++)
            if ((requirements.MemoryTypeBits & (1u << (int)i)) != 0 && (properties.MemoryTypes[(int)i].PropertyFlags & flags) == flags)
            { type = i; break; }
        if (type == null) throw new NotSupportedException("Memória de leitura dos pixels indisponível.");
        MemoryAllocateInfo allocation = new() { SType = StructureType.MemoryAllocateInfo, AllocationSize = requirements.Size, MemoryTypeIndex = type.Value };
        Check(_vk.AllocateMemory(_device, in allocation, null, out _readbackMemory));
        Check(_vk.BindBufferMemory(_device, _readbackBuffer, _readbackMemory, 0));
    }

    private GpuPreviewFrame ReadPixels(long frameNumber, double seconds, Action observe)
    {
        EnsureReadbackBuffer();
        Check(_vk.ResetCommandBuffer(_command, 0));
        CommandBufferBeginInfo begin = new() { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
        Check(_vk.BeginCommandBuffer(_command, in begin));
        ImageMemoryBarrier barrier = new() { SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit, DstAccessMask = AccessFlags.TransferReadBit,
            OldLayout = ImageLayout.ColorAttachmentOptimal, NewLayout = ImageLayout.TransferSrcOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = _image, SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1) };
        _vk.CmdPipelineBarrier(_command, PipelineStageFlags.ColorAttachmentOutputBit, PipelineStageFlags.TransferBit,
            0, 0, null, 0, null, 1, &barrier);
        BufferImageCopy copy = new() { ImageSubresource = new(ImageAspectFlags.ColorBit, 0, 0, 1), ImageExtent = new(_width, _height, 1) };
        _vk.CmdCopyImageToBuffer(_command, _image, ImageLayout.TransferSrcOptimal, _readbackBuffer, 1, in copy);
        // Restore the attachment layout for subsequent draws and final readback.
        ImageMemoryBarrier restore = barrier;
        restore.SrcAccessMask = AccessFlags.TransferReadBit;
        restore.DstAccessMask = AccessFlags.ColorAttachmentWriteBit;
        restore.OldLayout = ImageLayout.TransferSrcOptimal;
        restore.NewLayout = ImageLayout.ColorAttachmentOptimal;
        _vk.CmdPipelineBarrier(_command, PipelineStageFlags.TransferBit, PipelineStageFlags.ColorAttachmentOutputBit,
            0, 0, null, 0, null, 1, &restore);
        MemoryBarrier host = new() { SType = StructureType.MemoryBarrier,
            SrcAccessMask = AccessFlags.TransferWriteBit, DstAccessMask = AccessFlags.HostReadBit };
        _vk.CmdPipelineBarrier(_command, PipelineStageFlags.TransferBit, PipelineStageFlags.HostBit,
            0, 1, &host, 0, null, 0, null);
        Check(_vk.EndCommandBuffer(_command));
        Check(_vk.ResetFences(_device, 1, in _fence));
        var command = _command;
        SubmitInfo submit = new() { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &command };
        observe();
        Check(_vk.QueueSubmit(_queue, 1, in submit, _fence));
        WaitForFrame(observe);
        ulong size = (ulong)_width * _height * 4;
        void* data = null;
        Check(_vk.MapMemory(_device, _readbackMemory, 0, size, 0, &data));
        try
        {
            return new((int)_width, (int)_height, frameNumber, seconds, new ReadOnlySpan<byte>(data, checked((int)size)));
        }
        finally { _vk.UnmapMemory(_device, _readbackMemory); }
    }

    public static GpuRenderResult Run(string pciAddress, TimeSpan duration, double thermalLimit = 80,
        CancellationToken cancellation = default, Action<GpuRenderProgress>? progress = null, RenderGpu? expectedDevice = null,
        Action<GpuPreviewFrame>? preview = null)
    {
        if (duration < TimeSpan.FromMilliseconds(100) || duration > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(duration));
        var guard = new GpuThermalGuard(pciAddress, thermalLimit);
        cancellation.ThrowIfCancellationRequested();
        var devices = Enumerate().Where(d => d.PciAddress == pciAddress && d.Hardware).ToArray();
        if (devices.Length != 1) throw new NotSupportedException("GPU selecionada ausente ou ambígua; atualize a lista.");
        if (expectedDevice != null && devices[0] != expectedDevice)
            throw new InvalidOperationException("A GPU mudou desde a seleção; atualize a lista antes de iniciar.");
        using var sensor = new LinuxGpuTemperatureSource(new LinuxFiles());
        guard.Check(sensor.Read(devices[0]));
        using var renderer = new VulkanGpuRenderer(pciAddress, 640, 360, 32);
        if (renderer.Selected.Name != devices[0].Name || renderer.Selected.VendorId != devices[0].VendorId)
            throw new InvalidOperationException("Identificação da GPU mudou; atualize a lista.");
        var clock = Stopwatch.StartNew();
        long frames = 0;
        bool submitted = false;
        double lastTemperature = 0;
        TimeSpan nextPreview = TimeSpan.Zero;
        GpuPreviewFrame? lastPreview = null;
        void Observe()
        {
            cancellation.ThrowIfCancellationRequested();
            var reading = sensor.Read(renderer.Selected);
            try { guard.Check(reading); }
            catch (GpuThermalException ex) { ex.LoadStarted = submitted; throw; }
            lastTemperature = reading.Celsius!.Value;
        }
        while (clock.Elapsed < duration)
        {
            Observe();
            renderer.Draw((float)clock.Elapsed.TotalSeconds, Observe, () => submitted = true);
            frames++;
            if (preview != null && clock.Elapsed >= nextPreview)
            {
                lastPreview = renderer.ReadPixels(frames, clock.Elapsed.TotalSeconds, Observe);
                preview(lastPreview);
                nextPreview = clock.Elapsed + TimeSpan.FromMilliseconds(200);
            }
            progress?.Invoke(new(frames, clock.Elapsed.TotalSeconds, lastTemperature));
        }
        Observe();
        if (frames == 0) throw new InvalidOperationException("Prazo encerrado sem frame concluído.");
        double seconds = clock.Elapsed.TotalSeconds;
        var finalFrame = lastPreview?.FrameNumber == frames ? lastPreview
            : renderer.ReadPixels(frames, seconds, Observe);
        if (preview != null && finalFrame != lastPreview) preview(finalFrame);
        int pixels = finalFrame.NonBlackPixels();
        Observe();
        if (pixels == 0) throw new InvalidOperationException("O pipeline não produziu pixels visíveis.");
        return new(renderer.Selected.Name, pciAddress, frames, seconds, pixels);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_device.Handle != 0)
        {
            _vk.DeviceWaitIdle(_device);
            if (_readbackBuffer.Handle != 0) _vk.DestroyBuffer(_device, _readbackBuffer, null);
            if (_readbackMemory.Handle != 0) _vk.FreeMemory(_device, _readbackMemory, null);
            if (_fence.Handle != 0) _vk.DestroyFence(_device, _fence, null);
            if (_pool.Handle != 0) _vk.DestroyCommandPool(_device, _pool, null);
            if (_pipeline.Handle != 0) _vk.DestroyPipeline(_device, _pipeline, null);
            if (_layout.Handle != 0) _vk.DestroyPipelineLayout(_device, _layout, null);
            if (_framebuffer.Handle != 0) _vk.DestroyFramebuffer(_device, _framebuffer, null);
            if (_pass.Handle != 0) _vk.DestroyRenderPass(_device, _pass, null);
            if (_view.Handle != 0) _vk.DestroyImageView(_device, _view, null);
            if (_image.Handle != 0) _vk.DestroyImage(_device, _image, null);
            if (_memory.Handle != 0) _vk.FreeMemory(_device, _memory, null);
            _vk.DestroyDevice(_device, null);
        }
        if (_instance.Handle != 0) _vk.DestroyInstance(_instance, null);
        _vk.Dispose();
    }
}
