using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs the <c>BarrierBatch.SingleImageTransition</c> and
/// <c>BarrierBatch.LargeBatch_8x8x1</c> entries of issue 29: a sync2
/// pipeline-barrier call — single barrier or 64-barrier batch — reports
/// 0 B/op after warmup. The wrapper has no inline-array fast/slow split;
/// both overloads fold the caller's span through a method-local
/// <c>stackalloc</c>, so the comparison here is recording overhead per
/// barrier rather than two distinct allocation regimes.
/// </summary>
[MemoryDiagnoser]
public class PipelineBarrierBenchmarks
{
    private const int BatchSize     = 64; // 8 × 8 × 1 image
    private const int CallsPerInvoke = 256;

    private Instance          _instance = null!;
    private Device            _device   = null!;
    private CommandBufferPool _cmdPool  = null!;
    private Image             _image;

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
                Usage         = ImageUsage.ColorAttachment | ImageUsage.TransferSrc | ImageUsage.TransferDst,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        // Warm — first Begin grows the pool by one buffer; subsequent
        // begins/resets hit reuse and steady-state recording is alloc-free.
        var rec = _cmdPool.Begin();
        rec.End();
        rec.Dispose();
        _cmdPool.ResetForFrame();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cmdPool?.Dispose();
        _image.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void SingleImageTransition()
    {
        var bar = ImageBarrier.Transition(
            in _image,
            from:     VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            to:       VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
            srcStage: Stage.TopOfPipe,             srcAccess: Access.None,
            dstStage: Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite);

        using var rec = _cmdPool.Begin();
        for (int i = 0; i < CallsPerInvoke; i++)
            rec.PipelineBarrier(in bar);
        rec.End();

        _cmdPool.ResetForFrame();
    }

    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void LargeBatch_8x8x1()
    {
        var template = ImageBarrier.Transition(
            in _image,
            from:     VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            to:       VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
            srcStage: Stage.TopOfPipe,             srcAccess: Access.None,
            dstStage: Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite);

        Span<ImageBarrier> bars = stackalloc ImageBarrier[BatchSize];
        for (int i = 0; i < BatchSize; i++) bars[i] = template;

        // `scoped` narrows the recorder's safe-to-escape to this method
        // so the method-local stackalloc above can flow into
        // PipelineBarrier without tripping CS8350.
        using scoped var rec = _cmdPool.Begin();
        for (int i = 0; i < CallsPerInvoke; i++)
            rec.PipelineBarrier(default, default, bars);
        rec.End();

        _cmdPool.ResetForFrame();
    }
}
