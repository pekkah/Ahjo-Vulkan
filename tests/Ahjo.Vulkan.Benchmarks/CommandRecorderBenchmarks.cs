using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs the <c>CommandRecorder.RenderingPass100Cmds</c> entry of issue 29:
/// a dynamic-rendering pass with 100 inner recording calls reports 0 B/op
/// after warmup. The benchmark records <c>BeginRendering → 100×SetViewport →
/// EndRendering</c> inside a per-iteration <c>Begin/End/Reset</c> bracket.
/// SetViewport is the cheapest representative recording call — single
/// fixed-size struct, no span sizing, no descriptor lookups — so any
/// allocation noise is attributable to the recorder itself rather than to
/// the bound resource shape.
/// </summary>
[MemoryDiagnoser]
public class CommandRecorderBenchmarks
{
    private const int CommandsPerPass = 100;

    private Instance          _instance = null!;
    private Device            _device   = null!;
    private CommandBufferPool _cmdPool  = null!;
    private Image             _image;
    private ImageView         _view;

    [GlobalSetup]
    public void Setup()
    {
        _instance = Instance.Create(default);

        uint family = uint.MaxValue;
        var gpu = _instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    family = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        _device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });

        _cmdPool = new CommandBufferPool(_device, family);

        _image = _device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = 64, Height = 64, Depth = 1,
                MipLevels     = 1, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.ColorAttachment,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        _view = _image.CreateView(_device, new ImageViewDescription
        {
            ViewType   = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect     = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            LevelCount = 1, LayerCount = 1,
        });

        // Warm — fault in the pool's first command buffer + JIT the recording
        // path so the steady-state Begin/End pairs hit reuse and the
        // BeginRendering/EndRendering surfaces are tier-1+.
        RenderingPass100Cmds();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cmdPool?.Dispose();
        _view.Dispose();
        _image.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    [Benchmark]
    public void RenderingPass100Cmds()
    {
        var vp = new VkViewport { width = 64, height = 64, maxDepth = 1.0f };

        Span<ColorAttachment> color = stackalloc ColorAttachment[1];
        color[0] = new ColorAttachment
        {
            View    = _view,
            Layout  = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
            LoadOp  = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_DONT_CARE,
            StoreOp = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_DONT_CARE,
        };

        var info = new RenderingInfo
        {
            RenderArea       = new VkRect2D { extent = new VkExtent2D { width = 64, height = 64 } },
            LayerCount       = 1,
            ColorAttachments = color,
        };

        // `scoped` narrows the recorder's safe-to-escape to this method
        // so the method-local stackalloc above (carried inside info) can
        // flow into BeginRendering without tripping CS8350.
        using scoped var rec = _cmdPool.Begin();
        rec.BeginRendering(in info);
        for (int i = 0; i < CommandsPerPass; i++)
            rec.SetViewport(in vp);
        rec.EndRendering();
        rec.End();

        _cmdPool.ResetForFrame();
    }
}
