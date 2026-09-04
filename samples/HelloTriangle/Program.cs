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
    /// <summary>
    /// How long to sleep between retries while the window is minimized and the
    /// swapchain has no presentable extent. A real application blocks on its
    /// event queue instead; a sample that spun here would peg a core.
    /// </summary>
    private const int MinimizedPollMilliseconds = 16;

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
        // Enable validation + route findings to stderr. Acts as a
        // standing regression check — if a future wrapper change
        // re-introduces a windowed-loop spec violation it surfaces
        // here before we cut a release.
        using var instance = Instance.Create(new InstanceDescription
        {
            Extensions       = instanceExts,
            EnableValidation = true,
            DebugCallback    = OnValidationMessage,
        });

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
        // Non-presentability has to be STICKY, because nothing else in the
        // loop can rediscover it. After a minimize:
        //   * ConsumeResize() is false -- SdlWindow.PumpEvents only raises
        //     the flag for a non-zero size that differs from the last one,
        //     so neither the minimize nor a restore at the same size sets
        //     it; and
        //   * swap.Extent == window.Width/Height -- SdlWindow keeps its
        //     last non-zero size, and Swapchain.Recreate returns Minimized
        //     BEFORE assigning its extent field, so both sides keep their
        //     last good values and compare equal.
        // Every term of the test below therefore goes false while the
        // window is minimized, and without this flag the loop would fall
        // straight through to AcquireNextImage and throw.
        bool presentable = true;
        while (!window.ShouldClose)
        {
            window.PumpEvents();
            if (window.ShouldClose) break;

            // Consumed into a local BEFORE the || chain: inside it, a
            // short-circuit on an earlier term would swallow the event and
            // leave _resized set for a frame that no longer needs it.
            bool resized = window.ConsumeResize();

            // !presentable is the FIRST term, so a minimized loop keeps
            // re-entering the recreate path instead of falling through.
            if (!presentable || resized ||
                swap.Extent.width != window.Width || swap.Extent.height != window.Height)
            {
                presentable = TryRecreate(device, swap, ring, in surface, window);
                if (!presentable)
                {
                    // Still minimized. Sleep rather than spin; a real
                    // application blocks on the event queue instead.
                    System.Threading.Thread.Sleep(MinimizedPollMilliseconds);
                    continue;
                }
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
                presentable = TryRecreate(device, swap, ring, in surface, window);
                if (!presentable) System.Threading.Thread.Sleep(MinimizedPollMilliseconds);
                continue;
            }
            // Tested BEFORE the catch-all below: SurfaceLost is terminal, and
            // letting it reach a print-and-continue is the bug this guards.
            if (acq == AcquireResult.SurfaceLost)
            {
                ReportSurfaceLost();
                break;
            }
            // Everything left is Timeout or NotReady. Neither touches the
            // swapchain's state, and neither acquired an image or signalled
            // the acquire semaphore, so retrying next iteration is correct
            // and there is nothing to recycle.
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

                ReadOnlySpan<ColorAttachment> color = [new ColorAttachment
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

                // Swapchain-aware submit + matching present pull the
                // swapchain's per-image RenderingDone semaphore for
                // imageIndex — see issue #89 for why per-slot signaling
                // was wrong.
                fc.Submit(queue, ref rec, swap, imageIndex);
            }
            finally { rec.Dispose(); }

            var pres = swap.Present(queue, imageIndex);
            if (pres == AcquireResult.SurfaceLost)
            {
                ReportSurfaceLost();
                break;
            }
            // Present can only report Success, Suboptimal, OutOfDate or
            // SurfaceLost: Timeout and NotReady are gated `when fromAcquire`
            // in MapPresentationResult, so a present returning either is a
            // broken ICD and throws rather than mapping to a benign retry.
            // There is deliberately no catch-all on this side.
            if (pres is AcquireResult.OutOfDate or AcquireResult.Suboptimal)
            {
                // No sleep on the false leg: the loop top sees !presentable on
                // the next iteration and sleeps there. One un-slept iteration
                // is bounded.
                presentable = TryRecreate(device, swap, ring, in surface, window);
            }

            frame++;
            if (frame >= maxFrames) break;
        }

        device.WaitIdle();
        Console.WriteLine($"Rendered {frame} frames.");
        return 0;
    }

    /// <summary>
    /// <c>VK_ERROR_SURFACE_LOST_KHR</c> reached the frame loop. This is
    /// <b>terminal, not retryable</b>, and the sample stops.
    /// </summary>
    /// <remarks>
    /// <para><c>SurfaceLost</c> maps to <c>SwapchainState.Poisoned</c> without
    /// throwing, so unlike a hard error it flows back as an ordinary
    /// <see cref="AcquireResult"/> and has to be handled here or it falls
    /// through — and the next <c>AcquireNextImage</c> then throws out of
    /// <c>ThrowIfNotPresentable</c>, which rejects <c>Poisoned</c> exactly as it
    /// rejects <c>Minimized</c>.</para>
    /// <para><b>Do not turn this into a retry.</b> <c>Recreate</c> over the same
    /// <c>VkSurfaceKHR</c> cannot succeed: recovery means destroying and
    /// rebuilding the surface as well — a strict superset of the
    /// <c>OutOfDate</c> path, and the window system's business rather than this
    /// sample's. Routing it through <c>TryRecreate</c> would only fail
    /// differently.</para>
    /// <para>Exit code stays 0: a lost surface is an environment failure, not a
    /// defect in the sample.</para>
    /// </remarks>
    private static void ReportSurfaceLost()
    {
        Console.Error.WriteLine(
            "The window surface was lost (VK_ERROR_SURFACE_LOST_KHR — typically a display-driver " +
            "restart, a session switch or a monitor change). A swapchain over a lost surface cannot " +
            "be recreated, so this sample exits rather than retrying.");
    }

    /// <summary>
    /// Drains the device, rebuilds the swapchain at the window's current size
    /// and rotates any stale acquire semaphore. Returns <see langword="false"/>
    /// when the result is not presentable -- a minimized window, which is a
    /// legal state and not an error (#110).
    /// </summary>
    /// <remarks>
    /// Callers must test this return value rather than <c>swap.Extent</c>:
    /// <c>CreateOrRecreate</c> returns <c>Minimized</c> <i>before</i> assigning
    /// its extent field, so the property keeps its last good non-zero value and
    /// an extent test can never see the minimize.
    /// </remarks>
    private static bool TryRecreate(
        Device device, Swapchain swap, FrameRing ring, in Surface surface, SdlWindow window)
    {
        device.WaitIdle();
        SwapchainState state = swap.Recreate(new SwapchainDescription
        {
            Surface = surface,
            Width   = window.Width,
            Height  = window.Height,
        });
        ring.RecycleStaleAcquireSemaphores();
        return state == SwapchainState.Ready;
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

    /// <summary>
    /// Routes validation messages to stderr at WARN/ERROR severity
    /// (skipping INFO/VERBOSE which is mostly loader trace).
    /// </summary>
    private static void OnValidationMessage(DebugMessage msg)
    {
        bool isError = (msg.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0;
        bool isWarn  = (msg.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT) != 0;
        if (!isError && !isWarn) return;
        string tag = isError ? "ERROR" : "WARN ";
        Console.Error.WriteLine($"[VK {tag}] {msg.MessageIdName ?? "?"}: {msg.Message}");
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
