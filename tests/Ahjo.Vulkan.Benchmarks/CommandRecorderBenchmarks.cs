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
/// <para>ALSO covers the <c>CommandRecorder.CopyBuffer</c> multi-region
/// span-sizing path (issue 141): <c>CopyBuffer_8Regions</c> stays under the
/// recorder's 16-element threshold and exercises the <c>stackalloc</c>
/// branch, while <c>CopyBuffer_24Regions</c> spills past it and exercises
/// the <c>ArrayPool</c> rent/return branch in <c>RentForOverflow</c> plus
/// the <c>BufferCopyRegion.ToNative</c> loop. Both must report 0 B/op after
/// warmup — <c>ArrayPool.Shared.Rent</c> reuses a faulted-in bucket and does
/// not allocate managed memory in steady state.</para>
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
    private Buffer            _copySrc;
    private Buffer            _copyDst;

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

        // Device-local source/destination for the CopyBuffer canary. 64 KiB
        // comfortably holds 24 disjoint 256-byte regions (24×256 = 6 KiB). We
        // only RECORD into these — never map or submit — so AutoPreferDevice
        // is fine and no host-access flag is needed.
        _copySrc = _device.Allocator.CreateBuffer(
            new BufferDescription { Size = 64 * 1024, Usage = BufferUsage.TransferSrc },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
        _copyDst = _device.Allocator.CreateBuffer(
            new BufferDescription { Size = 64 * 1024, Usage = BufferUsage.TransferDst },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        // Warm — fault in the pool's first command buffer + JIT the recording
        // path so the steady-state Begin/End pairs hit reuse and the
        // BeginRendering/EndRendering surfaces are tier-1+.
        RenderingPass100Cmds();

        // Warm the CopyBuffer paths too: JIT the recording call and, for the
        // 24-region case, fault in the ArrayPool<VkBufferCopy2> bucket so the
        // measured run hits a cached rental (0 B/op) rather than a first-time
        // allocation.
        CopyBuffer_8Regions();
        CopyBuffer_24Regions();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cmdPool?.Dispose();
        _copySrc.Dispose();
        _copyDst.Dispose();
        _view.Dispose();
        _image.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    [Benchmark]
    public void RenderingPass100Cmds()
    {
        var vp = new VkViewport { width = 64, height = 64, maxDepth = 1.0f };

        // Collection expression, not stackalloc — this is the shape #209 makes
        // available to a consumer inside a render loop (a reusable InlineArray
        // local rather than a per-iteration localloc), so the row measures what
        // the samples now actually write.
        ReadOnlySpan<ColorAttachment> color =
        [
            new ColorAttachment
            {
                View    = _view,
                Layout  = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                LoadOp  = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_DONT_CARE,
                StoreOp = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_DONT_CARE,
            },
        ];

        var info = new RenderingInfo
        {
            RenderArea       = new VkRect2D { extent = new VkExtent2D { width = 64, height = 64 } },
            LayerCount       = 1,
            ColorAttachments = color,
        };

        // The recording surface is `readonly` (#209), so the method-local
        // stack span carried inside info flows into BeginRendering without
        // the caller declaring the recorder local `scoped`.
        using var rec = _cmdPool.Begin();
        rec.BeginRendering(in info);
        for (int i = 0; i < CommandsPerPass; i++)
            rec.SetViewport(in vp);
        rec.EndRendering();
        rec.End();

        _cmdPool.ResetForFrame();
    }

    /// <summary>
    /// 8 disjoint 256-byte regions → under <c>CopyBuffer</c>'s 16-element
    /// threshold, so this stays on the <c>stackalloc VkBufferCopy2[16]</c>
    /// branch of <c>RentForOverflow</c>. The whole region span is built into
    /// a method-local <c>stackalloc</c> and flows into the ref-struct
    /// <c>CopyBuffer</c> call because the recording surface is
    /// <c>readonly</c> (#209) — no <c>scoped</c> recorder local needed.
    /// No submit — record + reset only.
    /// </summary>
    [Benchmark]
    public void CopyBuffer_8Regions()
    {
        Span<BufferCopyRegion> regions = stackalloc BufferCopyRegion[8];
        for (int i = 0; i < regions.Length; i++)
            regions[i] = BufferCopyRegion.Of(size: 256, srcOffset: (ulong)i * 256, dstOffset: (ulong)i * 256);

        using var rec = _cmdPool.Begin();
        rec.CopyBuffer(in _copySrc, in _copyDst, regions);
        rec.End();

        _cmdPool.ResetForFrame();
    }

    /// <summary>
    /// 24 disjoint 256-byte regions → past <c>CopyBuffer</c>'s 16-element
    /// threshold, so this exercises the <c>ArrayPool&lt;VkBufferCopy2&gt;</c>
    /// rent/return overflow branch (and the longer
    /// <c>BufferCopyRegion.ToNative</c> loop). The bucket is faulted in by the
    /// <c>[GlobalSetup]</c> warm call, so steady state must stay 0 B/op — a
    /// regression in the rental/return discipline would surface as a non-zero
    /// Allocated column here.
    /// </summary>
    [Benchmark]
    public void CopyBuffer_24Regions()
    {
        Span<BufferCopyRegion> regions = stackalloc BufferCopyRegion[24];
        for (int i = 0; i < regions.Length; i++)
            regions[i] = BufferCopyRegion.Of(size: 256, srcOffset: (ulong)i * 256, dstOffset: (ulong)i * 256);

        using var rec = _cmdPool.Begin();
        rec.CopyBuffer(in _copySrc, in _copyDst, regions);
        rec.End();

        _cmdPool.ResetForFrame();
    }
}
