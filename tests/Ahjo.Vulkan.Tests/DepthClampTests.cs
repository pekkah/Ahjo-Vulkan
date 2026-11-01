using System.IO;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Renders <c>near_plane_tri.vert</c>, a triangle wholly in front of the near
/// plane, and reads the result back. The pixels are the oracle: a pipeline that
/// merely builds says nothing about whether the flag reached the rasterizer.
/// </summary>
public sealed class DepthClampTests
{
    private const uint W = 64, H = 64;

    [Fact]
    public void WithDepthClamp_KeepsTriangleInFrontOfNearPlane()
    {
        RenderResult result = Render(static b => b.WithDepthClamp());

        Assert.Equal(255, result.Center);
        Assert.Equal(0, result.Corner);
    }

    [Fact]
    public void WithDepthClampFalse_ClipsTriangleInFrontOfNearPlane()
    {
        RenderResult result = Render(static b => b.WithDepthClamp().WithDepthClamp(false));

        Assert.Equal(0, result.Center);
    }

    [Fact]
    public void DefaultBuilder_ClipsTriangleInFrontOfNearPlane()
    {
        RenderResult result = Render(static b => b);

        Assert.Equal(0, result.Center);
    }

    private delegate GraphicsPipelineBuilder Configure(GraphicsPipelineBuilder builder);

    private readonly record struct RenderResult(byte Center, byte Corner);

    private static RenderResult Render(Configure configure)
    {
        TestGate.RequireDriver();
        TestGate.RequireSpirv(VertSpvPath);
        TestGate.RequireSpirv(FragSpvPath);

        var errors = new List<DebugMessage>();
        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion       = VulkanVersion.V1_4,
            EnableValidation = VulkanEnvironment.HasValidationLayer,
            DebugCallback    = m =>
            {
                if ((m.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0)
                    lock (errors) errors.Add(m);
            },
        });

        using Device? device = TryCreateDepthClampDevice(instance, out uint family);
        TestGate.RequireDeviceFeature(device is not null, "Device does not advertise depthClamp.");

        using var image = device!.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = W, Height = H, Depth = 1,
                MipLevels     = 1, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.ColorAttachment | ImageUsage.TransferSrc,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
        using var view = image.CreateView(device, new ImageViewDescription
        {
            ViewType   = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect     = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            LevelCount = 1, LayerCount = 1,
        });
        using var download = device.Allocator.CreateBuffer(
            new BufferDescription { Size = W * H * 4, Usage = BufferUsage.TransferDst },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        using var vBlob  = SpirvBlob.Load(VertSpvPath);
        using var fBlob  = SpirvBlob.Load(FragSpvPath);
        using var vMod   = device.CreateShaderModule(vBlob.Words);
        using var fMod   = device.CreateShaderModule(fBlob.Words);
        using var layout = device.CreatePipelineLayout(default);

        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];
        using var pipeline = configure(device.BuildGraphicsPipeline())
            .WithStages(in vMod, in fMod)
            .WithDynamicRendering(colorFormats)
            .WithLayout(in layout)
            .Build();

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        Fence fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.PipelineBarrier(ImageBarrier.Transition(in image,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    srcStage: Stage.TopOfPipe,             srcAccess: Access.None,
                    dstStage: Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite));

                ReadOnlySpan<ColorAttachment> color =
                [
                    new ColorAttachment
                    {
                        View       = view,
                        Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                        LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                        StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                        ClearColor = new VkClearColorValue(),
                    },
                ];

                rec.BeginRendering(new RenderingInfo
                {
                    RenderArea       = new VkRect2D { extent = new VkExtent2D { width = W, height = H } },
                    LayerCount       = 1,
                    ColorAttachments = color,
                });
                rec.BindPipeline(in pipeline);
                rec.SetViewport(new VkViewport { x = 0, y = 0, width = W, height = H, minDepth = 0, maxDepth = 1 });
                rec.SetScissor(new VkRect2D { extent = new VkExtent2D { width = W, height = H } });
                rec.Draw(3, 1, 0, 0);
                rec.EndRendering();

                rec.PipelineBarrier(ImageBarrier.Transition(in image,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    srcStage: Stage.ColorAttachmentOutput, srcAccess: Access.ColorAttachmentWrite,
                    dstStage: Stage.AllTransfer,           dstAccess: Access.TransferRead));

                rec.CopyImageToBuffer(in image,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    in download,
                    BufferImageCopy.WholeImage(in image));

                // A fence wait does not make device writes visible to the host.
                // Without this and Invalidate below, a stale read of zeros would
                // pass the clipped tests falsely.
                BufferBarrier toHost = BufferBarrier.For(in download,
                    srcStage: Stage.AllTransfer, srcAccess: Access.TransferWrite,
                    dstStage: Stage.Host,        dstAccess: Access.HostRead);
                rec.PipelineBarrier(default, new ReadOnlySpan<BufferBarrier>(in toHost), default);

                Queue queue = device.GetQueue(family, queueIndex: 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(10)));
        }
        finally { fencePool.Release(fence); }

        lock (errors)
            Assert.True(errors.Count == 0,
                "Validation errors recorded: " + string.Join("; ", errors.ConvertAll(e => e.Message)));

        download.Invalidate();
        ReadOnlySpan<byte> pixels = download.AsReadOnlySpan<byte>();
        return new RenderResult(
            Center: pixels[(int)((H / 2 * W + W / 2) * 4)],
            Corner: pixels[0]);
    }

    /// <summary>
    /// Screens on <see cref="PhysicalDeviceInfo.Features"/>, the same check a
    /// caller makes to refuse a device without <c>depthClamp</c>.
    /// </summary>
    private static Device? TryCreateDepthClampDevice(Instance instance, out uint family)
    {
        uint f = uint.MaxValue;
        PhysicalDevice gpu;
        try
        {
            gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
            {
                if (info.Features.depthClamp == 0) return false;
                for (int i = 0; i < info.QueueFamilies.Length; i++)
                {
                    if (info.QueueFamilies[i].SupportsGraphics)
                    {
                        f = info.QueueFamilies[i].Index;
                        return true;
                    }
                }
                return false;
            });
        }
        catch (VulkanException ex) when (ex.Result == VkResult.VK_ERROR_INITIALIZATION_FAILED)
        {
            family = 0;
            return null;
        }

        family = f;
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(f, count: 1, priority: 1.0f)],
        });
    }

    private static string VertSpvPath =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", "near_plane_tri.vert.spv");

    private static string FragSpvPath =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", "triangle.frag.spv");
}
