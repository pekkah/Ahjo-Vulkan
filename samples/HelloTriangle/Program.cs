using Ahjo.Vulkan;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Samples.HelloTriangle;

/// <summary>
/// Issue 25 windowed sample: opens a window through the SDL3 shim,
/// builds a swapchain against it, and runs a FrameRing-driven present
/// loop drawing the same RGB triangle as <c>HeadlessTriangle</c>. Press
/// <kbd>Esc</kbd> to quit. Resizing the window triggers a swapchain
/// recreate at the next loop tick. Cross-platform (Windows + Wayland /
/// X11 on Linux + MoltenVK on macOS) thanks to SDL3's surface helper.
/// </summary>
internal static unsafe class Program
{
    private static int Main(string[] args)
    {
        // --frames N → close after N successfully presented frames. Lets
        // the sample double as a smoke test in CI without an Esc key.
        ulong maxFrames = ulong.MaxValue;
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "--frames" && ulong.TryParse(args[i + 1], out ulong n))
            {
                maxFrames = n;
                break;
            }
        }

        string shadersDir = Path.Combine(AppContext.BaseDirectory, "Shaders");
        string vertSpv    = Path.Combine(shadersDir, "triangle.vert.spv");
        string fragSpv    = Path.Combine(shadersDir, "triangle.frag.spv");
        if (!File.Exists(vertSpv) || !File.Exists(fragSpv))
        {
            Console.Error.WriteLine($"Missing compiled shaders. Expected:\n  {vertSpv}\n  {fragSpv}");
            return 2;
        }

        using var window = new SdlWindow("Ahjo.Vulkan — HelloTriangle (Esc to quit)", 1024, 768,
            hidden: false, resizable: true);

        // Ask SDL what Vulkan instance extensions the chosen video
        // driver needs — typically VK_KHR_surface + one platform
        // surface extension (Win32 / Wayland / Xlib / Metal). Cheaper
        // and more portable than guessing at the call site.
        Utf8Name[] instanceExts = SdlWindow.GetRequiredVulkanInstanceExtensions();
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });

        using var surface = window.CreateVulkanSurface(instance);
        using var device  = CreatePresentDevice(instance, in surface, out uint family);

        var swapDesc = new SwapchainDescription
        {
            Surface = surface,
            Width   = window.Width,
            Height  = window.Height,
        };
        using var swap = new Swapchain(device, in swapDesc);

        // ---- Pipeline ----
        using var vertBlob = SpirvBlob.Load(vertSpv);
        using var fragBlob = SpirvBlob.Load(fragSpv);
        using var vMod  = device.CreateShaderModule(vertBlob.Words);
        using var fMod  = device.CreateShaderModule(fragBlob.Words);
        using var layout = device.CreatePipelineLayout(default);

        VkFormat swapFormat = swap.Format;
        ReadOnlySpan<VkFormat> colorFormats = stackalloc VkFormat[] { swapFormat };
        using var pipeline = device.BuildGraphicsPipeline()
            .WithStages(in vMod, in fMod)
            .WithDynamicRendering(colorFormats)
            .WithLayout(in layout)
            .Build();

        using var ring = new FrameRing(device, framesInFlight: 2, queueFamily: family);
        var queue = device.GetQueue(family, 0);

        Console.WriteLine($"Swapchain: {swap.Format} {swap.Extent.width}x{swap.Extent.height}, {swap.ImageCount} images, {swap.PresentMode}");

        ulong frame = 0;
        while (!window.ShouldClose)
        {
            window.PumpEvents();
            if (window.ShouldClose) break;

            if (window.ConsumeResize() || swap.Extent.width != window.Width || swap.Extent.height != window.Height)
            {
                device.WaitIdle();
                swap.Recreate(new SwapchainDescription
                {
                    Surface = surface,
                    Width   = window.Width,
                    Height  = window.Height,
                });
                ring.RecycleStaleAcquireSemaphores();
                continue;
            }

            using var fc = ring.BeginFrame();

            var acq = swap.AcquireNextImage(fc.ImageAcquired, TimeSpan.FromSeconds(1), out uint imageIndex);
            // Suboptimal signals the semaphore per spec (just like
            // Success); only OutOfDate leaves it untouched. Marking
            // both signals lets RecycleStaleAcquireSemaphores below find
            // the stuck handle when we bail out without submitting.
            if (acq is AcquireResult.Success or AcquireResult.Suboptimal)
                fc.MarkImageAcquireSignaled();
            if (acq is AcquireResult.OutOfDate or AcquireResult.Suboptimal)
            {
                device.WaitIdle();
                swap.Recreate(new SwapchainDescription
                {
                    Surface = surface,
                    Width   = window.Width,
                    Height  = window.Height,
                });
                ring.RecycleStaleAcquireSemaphores();
                continue;
            }
            if (acq != AcquireResult.Success)
            {
                Console.Error.WriteLine($"AcquireNextImage: {acq}");
                continue;
            }

            ImageView swapView = swap.ImageViews[(int)imageIndex];

            var rec = fc.CommandBuffers.Begin();
            try
            {
                // Swapchain image is in PRESENT_SRC (or UNDEFINED on first
                // touch). Transition to COLOR_ATTACHMENT_OPTIMAL with
                // ColorAttachmentOutput as the dst stage — matches the
                // semaphore wait stage in FrameContext.Submit.
                RecordSwapchainBarrier(ref rec, swap, imageIndex,
                    from: VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    to:   VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    srcStage: Stage.TopOfPipe,             srcAccess: Access.None,
                    dstStage: Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite);

                ColorAttachment[] color = [new ColorAttachment
                {
                    View       = swapView,
                    Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                    StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                    ClearColor = ClearColor(0.05f, 0.07f, 0.10f, 1.0f),
                }];
                rec.BeginRendering(new RenderingInfo
                {
                    RenderArea       = new VkRect2D { extent = swap.Extent },
                    LayerCount       = 1,
                    ColorAttachments = color,
                });
                rec.SetViewport(new VkViewport
                {
                    x = 0, y = 0,
                    width  = swap.Extent.width,
                    height = swap.Extent.height,
                    minDepth = 0, maxDepth = 1,
                });
                rec.SetScissor(new VkRect2D { extent = swap.Extent });
                rec.BindPipeline(in pipeline);
                rec.Draw(vertexCount: 3);
                rec.EndRendering();

                RecordSwapchainBarrier(ref rec, swap, imageIndex,
                    from: VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    to:   VkImageLayout.VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
                    srcStage: Stage.ColorAttachmentOutput, srcAccess: Access.ColorAttachmentWrite,
                    dstStage: Stage.BottomOfPipe,          dstAccess: Access.None);

                fc.Submit(queue, ref rec);
            }
            finally { rec.Dispose(); }

            var pres = swap.Present(queue, imageIndex, fc.RenderingDone);
            if (pres is AcquireResult.OutOfDate or AcquireResult.Suboptimal)
            {
                device.WaitIdle();
                swap.Recreate(new SwapchainDescription
                {
                    Surface = surface,
                    Width   = window.Width,
                    Height  = window.Height,
                });
                ring.RecycleStaleAcquireSemaphores();
            }

            frame++;
            if (frame >= maxFrames) break;
        }

        device.WaitIdle();
        Console.WriteLine($"Rendered {frame} frames.");
        return 0;
    }

    private static void RecordSwapchainBarrier(
        ref CommandRecorder rec,
        Swapchain           swap,
        uint                imageIndex,
        VkImageLayout       from,
        VkImageLayout       to,
        Stage               srcStage, Access srcAccess,
        Stage               dstStage, Access dstAccess)
    {
        // Swapchain images aren't VMA-allocated, so there's no wrapper
        // Image we can hand to ImageBarrier.Transition. Build the
        // barrier directly from the raw image handle the swapchain
        // exposes via GetImageHandle.
        var barrier = new ImageBarrier
        {
            Image          = swap.GetImageHandle(imageIndex),
            SrcStage       = srcStage, SrcAccess = srcAccess,
            DstStage       = dstStage, DstAccess = dstAccess,
            OldLayout      = from,     NewLayout = to,
            Aspect         = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            BaseMipLevel   = 0, LevelCount = 1,
            BaseArrayLayer = 0, LayerCount = 1,
        };
        rec.PipelineBarrier(barrier);
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

    private static Device CreatePresentDevice(Instance instance, in Surface surface, out uint family)
    {
        // Picker can't capture the `in Surface` ref-struct param, so make
        // a local copy and capture the value-typed Surface instead.
        Surface local = surface;
        uint    chosen = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (!info.QueueFamilies[i].SupportsGraphics) continue;
                if (info.Device.SupportsPresent(info.QueueFamilies[i].Index, in local))
                {
                    chosen = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        family = chosen;

        Utf8Name[] deviceExts = [VulkanExtensions.KhrSwapchain];
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues     = [new QueueRequest(family, count: 1, priority: 1.0f)],
            Extensions = deviceExts,
        });
    }
}
