using Ahjo.Vulkan;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Utilities;

namespace Ahjo.Vulkan.Samples.HeadlessTriangle;

/// <summary>
/// Issue 25 cross-platform sample: render an RGB triangle to an
/// offscreen 512×512 RGBA8 image via dynamic rendering, copy the pixels
/// back through a host-visible buffer, and dump <c>triangle.png</c>
/// next to the executable. No window, no swapchain — exercises the
/// full wrapper composition without a platform surface.
/// </summary>
internal static unsafe class Program
{
    private const uint Width  = 512;
    private const uint Height = 512;

    private static int Main(string[] args)
    {
        string outPath = args.Length > 0 ? args[0]
            : Path.Combine(AppContext.BaseDirectory, "triangle.png");
        string shadersDir = Path.Combine(AppContext.BaseDirectory, "Shaders");
        string vertSpv    = Path.Combine(shadersDir, "triangle.vert.spv");
        string fragSpv    = Path.Combine(shadersDir, "triangle.frag.spv");
        if (!File.Exists(vertSpv) || !File.Exists(fragSpv))
        {
            Console.Error.WriteLine($"Missing compiled shaders. Expected:\n  {vertSpv}\n  {fragSpv}");
            return 2;
        }

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        // ---- Shader pipeline ----
        using var vertBlob = SpirvBlob.Load(vertSpv);
        using var fragBlob = SpirvBlob.Load(fragSpv);
        using var vMod  = device.CreateShaderModule(vertBlob.Words);
        using var fMod  = device.CreateShaderModule(fragBlob.Words);
        using var layout = device.CreatePipelineLayout(default);

        ReadOnlySpan<VkFormat> colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];
        using var pipeline = device.BuildGraphicsPipeline()
            .WithStages(in vMod, in fMod)
            .WithDynamicRendering(colorFormats)
            .WithLayout(in layout)
            .Build();

        // ---- Render target + readback ----
        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType   = VkImageType.VK_IMAGE_TYPE_2D,
                Format      = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width       = Width, Height = Height, Depth = 1,
                MipLevels   = 1, ArrayLayers = 1,
                Samples     = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling      = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage       = ImageUsage.ColorAttachment | ImageUsage.TransferSrc,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using var view = image.CreateView(device, new ImageViewDescription
        {
            ViewType     = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect       = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            BaseMipLevel = 0, LevelCount = 1, BaseArrayLayer = 0, LayerCount = 1,
        });

        const uint Bytes = Width * Height * 4;
        using var readback = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Bytes, Usage = BufferUsage.TransferDst },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        // ---- Record + submit ----
        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
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

                ColorAttachment[] color = [new ColorAttachment
                {
                    View       = view,
                    Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                    StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                    ClearColor = ClearColor(0.05f, 0.07f, 0.10f, 1.0f),
                }];

                rec.BeginRendering(new RenderingInfo
                {
                    RenderArea       = new VkRect2D { extent = new VkExtent2D { width = Width, height = Height } },
                    LayerCount       = 1,
                    ColorAttachments = color,
                });
                rec.SetViewport(new VkViewport { x = 0, y = 0, width = Width, height = Height, minDepth = 0, maxDepth = 1 });
                rec.SetScissor(new VkRect2D { extent = new VkExtent2D { width = Width, height = Height } });
                rec.BindPipeline(in pipeline);
                rec.Draw(vertexCount: 3);
                rec.EndRendering();

                rec.PipelineBarrier(ImageBarrier.Transition(in image,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    srcStage: Stage.ColorAttachmentOutput, srcAccess: Access.ColorAttachmentWrite,
                    dstStage: Stage.AllTransfer,           dstAccess: Access.TransferRead));

                rec.CopyImageToBuffer(in image,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    in readback,
                    BufferImageCopy.WholeImage(in image));

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            if (fence.Wait(TimeSpan.FromSeconds(10)) != WaitState.Signaled)
            {
                Console.Error.WriteLine("GPU work did not complete within 10 seconds.");
                return 3;
            }
        }
        finally { fencePool.Release(fence); }

        // ---- Pixels → PNG ----
        ReadOnlySpan<byte> pixels = readback.AsReadOnlySpan<byte>();
        PngWriter.Write(outPath, pixels, (int)Width, (int)Height);
        Console.WriteLine($"Wrote {outPath} ({Width}x{Height}, {pixels.Length:N0} bytes pre-encode).");
        return 0;
    }

    private static VkClearColorValue ClearColor(float r, float g, float b, float a)
    {
        var c = new VkClearColorValue();
        c.float32[0] = r;
        c.float32[1] = g;
        c.float32[2] = b;
        c.float32[3] = a;
        return c;
    }

    private static Device CreateGraphicsDevice(Instance instance, out uint family)
    {
        uint chosen = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    chosen = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        family = chosen;
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }
}
