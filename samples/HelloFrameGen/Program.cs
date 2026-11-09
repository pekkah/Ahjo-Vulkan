using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Ngx;
using Ahjo.Vulkan.Utilities;

namespace Ahjo.Vulkan.Samples.HelloFrameGen;

/// <summary>
/// A lit, textured cube under an orbiting camera, with a translucent HUD panel,
/// run through NVIDIA DLSS Frame Generation and presented twice per rendered
/// frame (generated, then real), with NVIDIA Reflex Low Latency and PC Latency
/// Stats wired in. <c>--mode off</c> is the control: the same frame, one present.
/// </summary>
/// <remarks>
/// <para>This is the worked call site for <c>docs/ngx-frame-generation-notes.md</c>
/// (#253). The wrapper records the evaluate; everything around it is the
/// application's: the HUD-less and UI targets, the layouts, the camera block,
/// and above all the presentation (#253 spec D5: the wrapper owns no present,
/// no pacer and no thread). Pacing here is <c>VK_PRESENT_MODE_FIFO_KHR</c>:
/// two back-to-back presents land one vblank apart.</para>
/// <para><b>Needs an NVIDIA RTX 40-series or newer GPU and a consumer-supplied
/// <c>nvngx_dlssg.dll</c></b> (#214). It does not need <c>nvngx_dlss.dll</c>:
/// the context is created with <c>RequiredFeatures = FrameGeneration</c>. CI
/// builds this sample and never runs it (#32). Without Frame Generation it
/// prints the wrapper's diagnosis verbatim and runs <c>--mode off</c>, or exits
/// 5 under <c>--require-fg</c>.</para>
/// <para><b>The frame loop allocates nothing</b> on the steady-state path: no
/// <c>T[]</c>, no lambdas, no LINQ, no interpolated strings, no console output.
/// The semaphore spans are stack locals through
/// <see cref="MemoryMarshal.CreateReadOnlySpan{T}(ref readonly T, int)"/>, the
/// <c>FrameContext.Submit</c> shape. The opt-in <c>--latency-report</c> line
/// formats a string every 120 frames (the HelloCube precedent), and the resize
/// path allocates freely; neither is the frame path.</para>
/// </remarks>
internal static unsafe class Program
{
    private const uint FramesInFlight = 2;
    private const int  MinimizedPollMilliseconds = 16;

    /// <summary>Capacity of the host present-timestamp log (#253 PR C step 19d
    /// fallback). Setup-time allocation; logging stops when full.</summary>
    private const int PresentLogCapacity = 1 << 16;

    // Validation messages fire from arbitrary driver threads; static counters
    // keep the callback closure captureless.
    private static int s_validationErrors;
    private static int s_validationWarnings;

    // Setup-path state the static ImmediateSubmit lambda reads; never touched
    // from the frame loop except by reading.
    private static NgxContext?                    s_ngx;
    private static DlssFrameGenerationFeature?    s_fg;
    private static DlssFrameGenerationDescription s_fgDescription;

    private static int Main(string[] args)
    {
        // ---- 1. Command line. -------------------------------------------
        if (!FrameGenOptions.TryParse(args, out FrameGenOptions options, out string? parseError))
        {
            Console.Error.WriteLine(parseError);
            return 2;
        }

        // ---- 2. Wrapper validation, BEFORE any handle exists. ------------
        AhjoValidation.Enabled = options.Validation;

        string cubeShader    = Path.Combine(AppContext.BaseDirectory, "Shaders", "cube.slang");
        string overlayShader = Path.Combine(AppContext.BaseDirectory, "Shaders", "overlay.slang");
        if (!File.Exists(cubeShader) || !File.Exists(overlayShader))
        {
            Console.Error.WriteLine($"Missing shaders. Expected: {cubeShader} and {overlayShader}");
            return 2;
        }

        // ---- 3. The NGX description. -------------------------------------
        var ngxDescription = new NgxDescription
        {
            ProjectId     = "8c1d4e7a-3b52-4f09-9a6e-71d2c5b0f3a8",  // this sample's own, fixed
            EngineVersion = "0.1.0-helloframegen",
            DlssSearchPaths = BuildSearchPaths(),
            // Frame Generation ALWAYS, and only it: this sample uses no Super
            // Resolution, so it must not require nvngx_dlss.dll (#253 step 17).
            RequiredFeatures = NgxFeatures.FrameGeneration,
            // LoggingLevel stays Off: NGX logging above Off allocates per
            // callback (src/Ahjo.Vulkan.Ngx/CLAUDE.md).
        };

        // ---- 4. Window. ---------------------------------------------------
        using var window = new SdlWindow("Ahjo.Vulkan — HelloFrameGen (Esc to quit)", 1920, 1080,
            hidden: false, resizable: true);
        if (options.Borderless)
            window.SetBorderlessFullscreen();

        // ---- 5. NGX instance extensions: the UNION of SR and FG. ----------
        // The order 5 -> 10 is NgxSupport's contract: instance extensions ->
        // instance -> physical device -> device extensions -> device ->
        // NgxContext.Create. Missing INSTANCE extensions access-violate inside
        // NVIDIA's client library; missing DEVICE extensions read exactly like
        // an unsupported GPU. Both features are queried and unioned even
        // though this sample only evaluates FG: the lists are NGX's answer for
        // one GPU and one driver, not a contract (docs/ngx-frame-generation-notes.md).
        bool wantsFg = options.Mode == FrameGenMode.Fg;
        Utf8Name[] ngxInstanceNames = [];
        if (wantsFg)
        {
            try
            {
                ngxInstanceNames = QueryInstanceExtensionUnion(in ngxDescription, out bool fgOk);
                if (!fgOk && !TryContinueWithoutFg(in options,
                        "NGX could not report the instance extensions Frame Generation requires.", out int code))
                {
                    return code;
                }
                wantsFg = fgOk;
            }
            catch (DllNotFoundException)
            {
                if (!TryContinueWithoutFg(in options,
                        "The ahjo_ngx shim is not present. Run ./tools/setup-ngx.ps1 to stage the NGX SDK and build it.",
                        out int code))
                {
                    return code;
                }
                wantsFg = false;
            }
        }

        // ---- 6. Instance. ------------------------------------------------
        using var instance = Instance.Create(new InstanceDescription
        {
            Extensions       = Union(SdlWindow.GetRequiredVulkanInstanceExtensions(), ngxInstanceNames),
            EnableValidation = options.Validation,
            DebugCallback    = options.Validation ? OnValidationMessage : null,
        });
        using var surface = window.CreateVulkanSurface(instance);

        // ---- 7. Physical device: graphics + present. ---------------------
        Surface localSurface = surface;
        uint    family       = uint.MaxValue;
        PhysicalDevice gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (!info.QueueFamilies[i].SupportsGraphics) continue;
                if (info.Device.SupportsPresent(info.QueueFamilies[i].Index, in localSurface))
                {
                    family = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });

        // ---- 8. NGX device extensions: the UNION of SR and FG. -----------
        Utf8Name[] ngxDeviceNames = [];
        if (wantsFg)
        {
            ngxDeviceNames = QueryDeviceExtensionUnion(gpu, in ngxDescription, out bool fgOk);
            if (!fgOk)
            {
                if (!TryContinueWithoutFg(in options,
                        "NGX could not report the device extensions Frame Generation requires on this GPU.", out int code))
                {
                    return code;
                }
                wantsFg = false;
            }
        }

        // ---- 9. Device. --------------------------------------------------
        // Reflex (#261 D2): VK_NV_low_latency2 plus its creation dependency
        // VK_KHR_present_id, only where advertised. No presentId feature push:
        // this sample passes no KHR present ids, and Reflex does not need them
        // (#261 D4/E4; docs/reflex-notes.md §2).
        bool reflex = gpu.SupportsExtension(VulkanExtensions.NvLowLatency2);
        Utf8Name[] deviceExtensions = reflex
            ? Union([VulkanExtensions.KhrSwapchain, VulkanExtensions.NvLowLatency2, VulkanExtensions.KhrPresentId], ngxDeviceNames)
            : Union([VulkanExtensions.KhrSwapchain], ngxDeviceNames);

        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues     = [new QueueRequest(family, count: 1, priority: 1.0f)],
            Extensions = deviceExtensions,
            // overlay.slang's two passes draw from SV_VertexID, which Slang
            // maps through the SPIR-V DrawParameters capability; that needs
            // shaderDrawParameters (Ahjo.Vulkan.Slang README, #168). It is not
            // one of the four structs the configurer hands out by ref, so it
            // goes on through the chain (the HeadlessTriangle shape).
            ConfigureFeatures = static (
                ref ChainBuilder<VkDeviceCreateInfo> chain,
                ref VkPhysicalDeviceFeatures2        _,
                ref VkPhysicalDeviceVulkan12Features _,
                ref VkPhysicalDeviceVulkan13Features _,
                ref VkPhysicalDeviceVulkan14Features _) =>
            {
                ref VkPhysicalDeviceVulkan11Features f11 = ref chain.Push<VkPhysicalDeviceVulkan11Features>();
                f11.shaderDrawParameters = 1;
            },
        });

        return Run(window, in surface, device, family, in options, in ngxDescription,
                   cubeShader, overlayShader, wantsFg);
    }

    private static int Run(
        SdlWindow window,
        in Surface surface,
        Device device,
        uint family,
        in FrameGenOptions options,
        in NgxDescription ngxDescription,
        string cubeShader,
        string overlayShader,
        bool wantsFg)
    {
        var queue = device.GetQueue(family, 0);

        // ---- 10. Swapchain. ----------------------------------------------
        // UNORM only: the shaders encode sRGB themselves, and blitting an
        // already-encoded UNORM source into an _SRGB destination would encode
        // a second time. TransferDst makes the present blits legal.
        //
        // The default image count is enough: the D5 loop holds at most one
        // acquired image at a time (acquire i0 -> present i0 -> acquire i1 ->
        // present i1).
        //
        // LowLatency follows the device, and the SAME value goes to every
        // Recreate (a mismatch throws, #261 D3).
        ReadOnlySpan<VkSurfaceFormatKHR> preferred =
        [
            new() { format = VkFormat.VK_FORMAT_B8G8R8A8_UNORM, colorSpace = VkColorSpaceKHR.VK_COLOR_SPACE_SRGB_NONLINEAR_KHR },
            new() { format = VkFormat.VK_FORMAT_R8G8B8A8_UNORM, colorSpace = VkColorSpaceKHR.VK_COLOR_SPACE_SRGB_NONLINEAR_KHR },
        ];
        var swapDesc = new SwapchainDescription
        {
            Surface              = surface,
            Width                = window.Width,
            Height               = window.Height,
            PreferredFormats     = preferred,
            PreferredPresentMode = options.PresentMode,
            ImageUsage           = ImageUsage.ColorAttachment | ImageUsage.TransferDst,
            LowLatency           = device.IsNvLowLatency2Enabled,
        };
        using var swap = new Swapchain(device, in swapDesc);

        Console.WriteLine($"Swapchain: {swap.Format} {swap.Extent.width}x{swap.Extent.height}, " +
            $"{swap.ImageCount} images, {swap.PresentMode}, {(options.Borderless ? "borderless" : "windowed")}");

        // ---- 11. Reflex mode, once, after swapchain creation. -------------
        // The wrapper re-applies it after every Recreate; the sample does not.
        // --reflex-interval-us goes into the SAME call, as MinimumIntervalUs
        // (#253 plan step 17b, spec F5).
        if (options.DiagnosticFgWithoutReflex)
            Console.WriteLine("DIAGNOSTIC: Frame Generation with Reflex off violates the RTX UI Developer Guidelines (UI l. 149-150); measurement only.");
        if (options.SkipSleep)
            Console.WriteLine("Reflex: sleep skipped (diagnostic; not a valid Reflex integration)");
        if (swap.IsLowLatencyEnabled)
        {
            uint intervalUs = ResolveSleepInterval(window, in options, wantsFg);
            var mode = new LatencySleepMode(options.Reflex.LowLatency, options.Reflex.Boost, intervalUs);
            swap.SetLatencySleepMode(in mode);
            Console.WriteLine($"Reflex: {options.ReflexName} ({mode})");
        }
        else
        {
            // SetLatencySleepMode throws on a swapchain that is not opted in,
            // by design: the Reflex UI is hidden on other vendors (Reflex
            // l. 1031). Sleep and markers still run; PCL still emits.
            Console.WriteLine("Reflex: unavailable on this GPU — PCL markers only");
        }

        // ---- 12. NGX context: RequiredFeatures = FrameGeneration. --------
        if (wantsFg)
        {
            try
            {
                s_ngx = NgxContext.Create(device, in ngxDescription);
                Console.WriteLine($"DLSS Frame Generation available: MaxGeneratedFrames = {s_ngx.MaxGeneratedFrames}.");
            }
            catch (NgxException ex)
            {
                // Printed VERBATIM, including the typed
                // NgxFeatureLibraryNotFoundException's file and directory list:
                // that diagnosis is the reason the wrapper throws at all.
                Console.Error.WriteLine(ex.Message);
                if (!TryContinueWithoutFg(in options, null, out int code)) return code;
                s_ngx = null;
                wantsFg = false;
            }
        }
        bool fg = wantsFg;

        using var scene    = new CubeScene(device, family, FramesInFlight);
        using var pipeline = new CubePipeline(device, cubeShader,
            FrameGenTargets.ColorFormat, FrameGenTargets.MotionFormat, FrameGenTargets.DepthFormat);
        using var overlay  = new OverlayPipelines(device, overlayShader,
            FrameGenTargets.UiFormat, FrameGenTargets.ColorFormat);

        using var ring      = new FrameRing(device, framesInFlight: FramesInFlight, queueFamily: family);
        using var setupPool = new CommandBufferPool(device, family);

        // The second acquire per frame needs its own binary semaphore per
        // slot: FrameContext.ImageAcquired is taken by the real frame's
        // acquire. Sample-owned (#253 spec D5, "why not multi-acquire slots").
        using var semaphores   = new SemaphorePool(device);
        var extraAcquire = new BinarySemaphore[FramesInFlight];
        var extraPending = new bool[FramesInFlight];
        for (int i = 0; i < extraAcquire.Length; i++) extraAcquire[i] = semaphores.AcquireBinary();

        var    targets         = new FrameGenTargets?[FramesInFlight];
        Buffer readbackGen     = default;
        Buffer readbackReal    = default;
        long[] presentLog      = new long[PresentLogCapacity];
        int    presentLogCount = 0;

        int exitCode = 0;
        PclStats.Initialize(new PclStatsOptions());
        window.EnablePclPingHook();

        try
        {
            if (pipeline.Failed || overlay.Failed) return 2;

            RebuildForExtent(device, queue, setupPool, in options, swap.Extent, fg,
                             targets, ref readbackGen, ref readbackReal);

            Console.WriteLine(fg
                ? "Mode fg: two presents per rendered frame (generated, then real)."
                : "Mode off: one present per rendered frame (the control).");

            var clock = Stopwatch.StartNew();
            Span<LatencyTimings> timings = stackalloc LatencyTimings[64];

            // One frame id per rendered frame, starting at 1 (0 throws), shared
            // by every Reflex marker, every PCL marker and BackbufferFrameId.
            // Never reused, not even for an abandoned frame.
            ulong frameId        = 0;
            ulong renderedFrames = 0;
            ulong presents       = 0;
            ulong sleepTimeouts  = 0;
            // --latency-report window accumulators (#253 plan step 19j):
            // Stopwatch ticks only, no allocation.
            var window_ = new ReportWindow { StartTicks = Stopwatch.GetTimestamp() };
            bool  captured       = false;
            bool  rebuildPending = false;
            bool  resetNext      = true;
            bool  havePrevious   = false;
            Matrix4x4 previousMvp      = Matrix4x4.Identity;
            Matrix4x4 previousViewProj = Matrix4x4.Identity;
            long throttleTicks = options.ThrottleMs * Stopwatch.Frequency / 1000;

            while (!window.ShouldClose)
            {
                frameId++;
                ulong n = frameId;

                // ---- Reflex sleep: once per RENDERED frame, before input. --
                // A no-op returning Signaled on a swapchain that is not opted in.
                // --skip-sleep is the diagnostic control only (step 19j J3).
                if (!options.SkipSleep)
                {
                    long sleepStart = Stopwatch.GetTimestamp();
                    if (swap.LatencySleep(TimeSpan.FromSeconds(1)) == WaitState.Timeout)
                        sleepTimeouts++;
                    window_.SleepTicks += Stopwatch.GetTimestamp() - sleepStart;
                }

                // SimulationStart emits a pending PCL ping first (#261 D6).
                swap.SetLatencyMarker(LatencyMarker.SimulationStart, n);
                window.PumpEvents();
                if (window.ConsumeLeftClick())
                    swap.SetLatencyMarker(LatencyMarker.TriggerFlash, n);
                if (window.ShouldClose)
                {
                    swap.SetLatencyMarker(LatencyMarker.SimulationEnd, n);
                    break;
                }

                // Simulation: the camera. --freeze holds the clock, so the
                // previous matrices equal the current ones and every motion
                // vector is zero.
                float seconds = options.Freeze ? 1.0f : (float)clock.Elapsed.TotalSeconds;
                swap.SetLatencyMarker(LatencyMarker.SimulationEnd, n);

                // Terminal first (#222): a lost surface is never recoverable.
                if (swap.State is SwapchainState.SurfaceLost)
                {
                    ReportSurfaceLost();
                    break;
                }

                bool resized = window.ConsumeResize();
                if (swap.State != SwapchainState.Ready || resized ||
                    swap.Extent.width != window.Width || swap.Extent.height != window.Height)
                {
                    if (!TryRecreate(device, swap, ring, semaphores, extraAcquire, extraPending,
                                     in surface, window, preferred, in options))
                    {
                        System.Threading.Thread.Sleep(MinimizedPollMilliseconds);
                        continue;
                    }
                    rebuildPending = true;
                }

                if (rebuildPending)
                {
                    RebuildForExtent(device, queue, setupPool, in options, swap.Extent, fg,
                                     targets, ref readbackGen, ref readbackReal);
                    rebuildPending = false;
                    resetNext      = true;
                    havePrevious   = false;
                }

                swap.SetLatencyMarker(LatencyMarker.RenderSubmitStart, n);
                long beginFrameStart = Stopwatch.GetTimestamp();
                using var fc = ring.BeginFrame();
                window_.BeginFrameTicks += Stopwatch.GetTimestamp() - beginFrameStart;
                uint slotIndex = fc.SlotIndex;
                FrameGenTargets slot = targets[slotIndex]!;

                // ---- Acquire i0: the image this frame presents FIRST. ------
                // Generated frame under fg, on the sample's per-slot semaphore;
                // the one and only image under off, on the slot's own. Acquired
                // before recording rather than mid-recording: the GPU-side
                // order is fixed by the submit's wait either way, and an
                // acquire failure then abandons a frame with nothing recorded.
                BinarySemaphore acquire0 = fg ? extraAcquire[slotIndex] : fc.ImageAcquired;
                long acquire0Start = Stopwatch.GetTimestamp();
                var acq0 = swap.AcquireNextImage(acquire0, TimeSpan.FromSeconds(1), out uint i0);
                window_.Acquire0Ticks += Stopwatch.GetTimestamp() - acquire0Start;
                if (acq0 is AcquireResult.Success or AcquireResult.Suboptimal)
                {
                    if (fg) extraPending[slotIndex] = true;
                    else    fc.MarkImageAcquireSignaled();
                }
                if (acq0 != AcquireResult.Success)
                {
                    // Abandoned frame: close it under its own id; the next
                    // iteration takes a fresh one. OutOfDate/Suboptimal
                    // recreate; Timeout/NotReady retry; SurfaceLost is caught
                    // at the loop top.
                    swap.SetLatencyMarker(LatencyMarker.RenderSubmitEnd, n);
                    if (acq0 is AcquireResult.OutOfDate or AcquireResult.Suboptimal)
                    {
                        if (TryRecreate(device, swap, ring, semaphores, extraAcquire, extraPending,
                                        in surface, window, preferred, in options))
                            rebuildPending = true;
                        else
                            System.Threading.Thread.Sleep(MinimizedPollMilliseconds);
                    }
                    continue;
                }

                // ---- Matrices and the camera block. -----------------------
                uint  width  = slot.Width, height = slot.Height;
                float aspect = height == 0 ? 1f : width / (float)height;
                BuildMatrices(seconds, aspect, out Matrix4x4 model, out Matrix4x4 view, out Matrix4x4 proj);
                Matrix4x4 viewProj = view * proj;
                Matrix4x4 mvp      = model * viewProj;

                // First frame, and after every rebuild: previous == current,
                // so the motion vectors are zero. Paired with Reset = true.
                if (!havePrevious)
                {
                    previousMvp      = mvp;
                    previousViewProj = viewProj;
                    havePrevious     = true;
                }

                // Row-vector composition throughout, the docs/ngx-notes.md §4
                // form: clip -> view -> world -> previous view -> previous clip.
                Matrix4x4.Invert(viewProj, out Matrix4x4 invViewProj);
                Matrix4x4.Invert(previousViewProj, out Matrix4x4 invPrevViewProj);
                Matrix4x4 clipToPrevClip = invViewProj * previousViewProj;
                Matrix4x4 prevClipToClip = invPrevViewProj * viewProj;

                scene.WriteUniforms(slotIndex, new CubeScene.FrameUniforms
                {
                    CurrentMvp     = mvp,
                    PreviousMvp    = previousMvp,
                    Model          = model,
                    ClipToPrevClip = clipToPrevClip,
                    RenderExtent   = new Vector2(width, height),
                });

                bool captureThisFrame = options.Captures && renderedFrames + 1 == options.MaxFrames;

                // ---- rec1: scene, UI, composite, evaluate, blit -> i0. -----
                var rec1 = fc.CommandBuffers.Begin();
                try
                {
                    slot.RecordPreRenderBarriers(ref rec1);
                    RecordScene(ref rec1, slot, scene, pipeline, slotIndex);
                    RecordUi(ref rec1, slot, overlay);
                    slot.RecordPreCompositeBarriers(ref rec1);
                    RecordComposite(ref rec1, slot, overlay);

                    Image firstSource;
                    if (fg)
                    {
                        slot.RecordPreEvaluateBarriers(ref rec1);

                        Matrix4x4.Invert(proj, out Matrix4x4 clipToView);
                        Matrix4x4.Invert(view, out Matrix4x4 viewToWorld);
                        DlssFrameGenerationCamera camera = BuildCamera(
                            in options, in proj, in clipToView, in clipToPrevClip, in prevClipToClip,
                            in viewToWorld, aspect);

                        bool normalized = options.MvecScale == MvecScaleMode.Normalized;
                        s_fg!.Evaluate(ref rec1, new DlssFrameGenerationInputs
                        {
                            Backbuffer         = slot.NgxBackbuffer,
                            Depth              = slot.NgxDepth,
                            MotionVectors      = slot.NgxMotionVectors,
                            Hudless            = slot.NgxHudless,
                            UiColorAndAlpha    = slot.NgxUi,
                            OutputInterpolated = slot.NgxOutputInterpolated,
                            OutputReal         = slot.NgxOutputReal,
                            Camera             = camera,
                            // No jitter: no Super Resolution in this sample
                            // (#253 PR C step 19c: "0, not exercised").
                            JitterOffsetX      = 0f,
                            JitterOffsetY      = 0f,
                            MotionVectorScaleX = normalized ? 1f / width  : 1f,
                            MotionVectorScaleY = normalized ? 1f / height : 1f,
                            Reset              = resetNext,
                            // Standard depth (near 0, far 1), LDR, and the
                            // vectors include the camera's motion.
                            DepthInverted        = false,
                            ColorBuffersHdr      = false,
                            CameraMotionIncluded = true,
                            BackbufferFrameId    = n,
                        });

                        // The evaluate clobbers the bound pipeline, descriptor
                        // sets and dynamic state (DLSS-FG guide l. 302-303).
                        // Nothing draws after it here, so there is nothing to
                        // rebind; a renderer that does must rebind all of it.
                        slot.RecordPostEvaluateBarriers(ref rec1);
                        firstSource = slot.OutputInterpolated;

                        if (captureThisFrame)
                        {
                            captured = true;
                            if (!readbackGen.IsNull)
                                rec1.CopyImageToBuffer(in slot.OutputInterpolated,
                                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, in readbackGen,
                                    BufferImageCopy.WholeImage(in slot.OutputInterpolated));
                            if (!readbackReal.IsNull)
                                rec1.CopyImageToBuffer(in slot.OutputReal,
                                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, in readbackReal,
                                    BufferImageCopy.WholeImage(in slot.OutputReal));
                        }
                    }
                    else
                    {
                        slot.RecordBackbufferToTransferSource(ref rec1);
                        firstSource = slot.Backbuffer;

                        if (captureThisFrame)
                        {
                            captured = true;
                            if (!readbackReal.IsNull)
                                rec1.CopyImageToBuffer(in slot.Backbuffer,
                                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, in readbackReal,
                                    BufferImageCopy.WholeImage(in slot.Backbuffer));
                        }
                    }

                    RecordPresentBlit(ref rec1, swap, i0, in firstSource);
                    resetNext = false;

                    if (fg)
                    {
                        // Waits on the extra acquire, signals the per-image
                        // RenderingDone that Present(queue, i0) waits on. No
                        // fence: rec2's fc.Submit below arms the slot fence,
                        // and a fence signal covers every earlier submission
                        // on the queue (#253 spec E6).
                        var wait   = new SemaphoreSubmit(extraAcquire[slotIndex], Stage.AllTransfer);
                        var signal = new SemaphoreSubmit(swap.GetRenderingDoneFor(i0), Stage.AllCommands);
                        queue.Submit2(ref rec1, default(Fence),
                            MemoryMarshal.CreateReadOnlySpan(ref wait, 1),
                            MemoryMarshal.CreateReadOnlySpan(ref signal, 1));
                        extraPending[slotIndex] = false;
                    }
                    else
                    {
                        // Both stage overrides matter, as in HelloDlaa: the
                        // only write to the swapchain image is a blit.
                        fc.Submit(queue, ref rec1, swap, i0,
                            imageAcquireWaitStage:    Stage.AllTransfer,
                            renderingDoneSignalStage: Stage.AllCommands);
                    }
                }
                finally { rec1.Dispose(); }

                // --throttle-ms: force the base rate down (#253 spec F3's
                // "B·M < R" row). A Stopwatch spin, not Thread.Sleep, so the
                // interval is precise.
                if (throttleTicks > 0)
                {
                    long until = Stopwatch.GetTimestamp() + throttleTicks;
                    while (Stopwatch.GetTimestamp() < until) { }
                }

                // ---- Present(s). ------------------------------------------
                swap.SetLatencyMarker(LatencyMarker.PresentStart, n);
                AcquireResult pres;
                if (fg)
                {
                    // The generated frame, "as soon as EvaluateFeature
                    // completes" (DLSS-FG guide). Both presents carry frame id
                    // N through the out-of-band pair (#261 D5).
                    swap.SetLatencyMarker(LatencyMarker.OutOfBandPresentStart, n);
                    long present0Start = Stopwatch.GetTimestamp();
                    pres = swap.Present(queue, i0);
                    window_.Present0Ticks += Stopwatch.GetTimestamp() - present0Start;
                    swap.SetLatencyMarker(LatencyMarker.OutOfBandPresentEnd, n);
                    LogPresent(presentLog, ref presentLogCount);
                    presents++;

                    if (pres is AcquireResult.Success)
                    {
                        // ---- Acquire i1 and present the real frame. --------
                        long acquire1Start = Stopwatch.GetTimestamp();
                        var acq1 = swap.AcquireNextImage(fc.ImageAcquired, TimeSpan.FromSeconds(1), out uint i1);
                        window_.Acquire1Ticks += Stopwatch.GetTimestamp() - acquire1Start;
                        window_.Acquire1Count++;
                        if (acq1 is AcquireResult.Success or AcquireResult.Suboptimal)
                            fc.MarkImageAcquireSignaled();

                        if (acq1 == AcquireResult.Success)
                        {
                            var rec2 = fc.CommandBuffers.Begin();
                            try
                            {
                                RecordPresentBlit(ref rec2, swap, i1, in slot.OutputReal);
                                fc.Submit(queue, ref rec2, swap, i1,
                                    imageAcquireWaitStage:    Stage.AllTransfer,
                                    renderingDoneSignalStage: Stage.AllCommands);
                            }
                            finally { rec2.Dispose(); }

                            swap.SetLatencyMarker(LatencyMarker.OutOfBandPresentStart, n);
                            long present1Start = Stopwatch.GetTimestamp();
                            pres = swap.Present(queue, i1);
                            window_.Present1Ticks += Stopwatch.GetTimestamp() - present1Start;
                            swap.SetLatencyMarker(LatencyMarker.OutOfBandPresentEnd, n);
                            LogPresent(presentLog, ref presentLogCount);
                            presents++;
                        }
                        else
                        {
                            // rec1 was submitted without a fence and nothing
                            // armed the slot's: TryRecreate drains the device
                            // (WaitIdle) before anything is reused.
                            pres = acq1 is AcquireResult.Timeout or AcquireResult.NotReady
                                ? AcquireResult.OutOfDate
                                : acq1;
                        }
                    }
                }
                else
                {
                    long present0Start = Stopwatch.GetTimestamp();
                    pres = swap.Present(queue, i0);
                    window_.Present0Ticks += Stopwatch.GetTimestamp() - present0Start;
                    LogPresent(presentLog, ref presentLogCount);
                    presents++;
                }
                swap.SetLatencyMarker(LatencyMarker.PresentEnd, n);
                swap.SetLatencyMarker(LatencyMarker.RenderSubmitEnd, n);

                if (pres is AcquireResult.OutOfDate or AcquireResult.Suboptimal)
                {
                    if (TryRecreate(device, swap, ring, semaphores, extraAcquire, extraPending,
                                    in surface, window, preferred, in options))
                        rebuildPending = true;
                }

                previousMvp      = mvp;
                previousViewProj = viewProj;
                renderedFrames++;

                window_.Frames++;
                if (options.LatencyReport != 0 && renderedFrames % options.LatencyReport == 0)
                {
                    ReportLatency(swap, timings, presents, in window_, sleepTimeouts);
                    window_ = new ReportWindow { StartTicks = Stopwatch.GetTimestamp(), PresentsAtStart = presents };
                }

                if (renderedFrames >= options.MaxFrames) break;
            }

            device.WaitIdle();
            double elapsed = clock.Elapsed.TotalSeconds;
            Console.WriteLine(
                $"Rendered {renderedFrames} frames in {elapsed:F1} s: rendered rate {renderedFrames / elapsed:F2} fps " +
                $"(BackbufferFrameId delta per second), {presents} presents ({presents / elapsed:F2} per second), " +
                $"presents per rendered frame {(renderedFrames == 0 ? 0 : (double)presents / renderedFrames):F3}, " +
                $"frame ids used {frameId}, Reflex sleep timeouts {sleepTimeouts}.");
            ReportPresentIntervals(presentLog, presentLogCount, fg);

            WriteCaptures(in options, captured, fg, readbackGen, readbackReal, targets[0]);
        }
        finally
        {
            device.WaitIdle();
            PclStats.Shutdown();
            s_fg?.Dispose();
            s_fg = null;
            s_ngx?.Dispose();
            s_ngx = null;
            for (int i = 0; i < targets.Length; i++) targets[i]?.Dispose();
            readbackGen.Dispose();
            readbackReal.Dispose();

            Console.WriteLine(
                $"Validation: {System.Threading.Volatile.Read(ref s_validationErrors)} error(s), " +
                $"{System.Threading.Volatile.Read(ref s_validationWarnings)} warning(s).");
        }

        return System.Threading.Volatile.Read(ref s_validationErrors) == 0 ? exitCode : 3;
    }

    // ---------------------------------------------------------------------
    //  Recording.
    // ---------------------------------------------------------------------

    /// <summary>
    /// The cube, then the far-plane background, into Hudless + motion vectors
    /// + depth.
    /// </summary>
    private static void RecordScene(
        ref CommandRecorder rec, FrameGenTargets slot, CubeScene scene, CubePipeline pipeline, uint slotIndex)
    {
        ReadOnlySpan<ColorAttachment> color =
        [
            new ColorAttachment
            {
                View       = slot.HudlessView,
                Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                ClearColor = ClearColor(0.05f, 0.06f, 0.09f, 1.0f),
            },
            new ColorAttachment
            {
                View       = slot.MotionView,
                Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                ClearColor = ClearColor(0f, 0f, 0f, 0f),
            },
        ];
        var renderArea = new VkRect2D { extent = new VkExtent2D { width = slot.Width, height = slot.Height } };
        rec.BeginRendering(new RenderingInfo
        {
            RenderArea       = renderArea,
            LayerCount       = 1,
            ColorAttachments = color,
            DepthAttachment  = new DepthAttachment
            {
                View       = slot.DepthView,
                Layout     = VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL,
                LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                // Far = 1.0: standard depth, so DepthInverted stays false.
                ClearDepth = 1.0f,
            },
        });
        SetViewportAndScissor(ref rec, in renderArea);

        Buffer uniformBuffer = scene.Uniforms(slotIndex);
        Sampler sampler      = scene.Sampler;
        ReadOnlySpan<DescriptorWrite> writes =
        [
            DescriptorWrite.Buffer(
                binding: 0, arrayElement: 0,
                VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                BufferDescriptorWrite.Of(in uniformBuffer)),
            DescriptorWrite.CombinedImageSampler(
                binding: 1, arrayElement: 0,
                ImageDescriptorWrite.Of(in sampler, in scene.TextureView,
                    VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL)),
        ];

        rec.BindPipeline(in pipeline.Pipeline);
        rec.PushDescriptorSet(VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS, in pipeline.Layout, set: 0, writes);
        ReadOnlySpan<Buffer> vertexBuffers = [scene.VertexBuffer];
        rec.BindVertexBuffers(0, vertexBuffers);
        rec.BindIndexBuffer(in scene.IndexBuffer, 0, VkIndexType.VK_INDEX_TYPE_UINT16);
        rec.DrawIndexed(scene.IndexCount);

        // Same layout, same descriptors: the background reads clipToPrevClip.
        rec.BindPipeline(in pipeline.BackgroundPipeline);
        rec.PushDescriptorSet(VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS, in pipeline.Layout, set: 0, writes);
        rec.Draw(3);

        rec.EndRendering();
    }

    /// <summary>One premultiplied translucent quad into Ui, cleared to 0 so
    /// every non-UI pixel is 0 (DLSS-FG guide l. 750).</summary>
    private static void RecordUi(ref CommandRecorder rec, FrameGenTargets slot, OverlayPipelines overlay)
    {
        ReadOnlySpan<ColorAttachment> color =
        [
            new ColorAttachment
            {
                View       = slot.UiView,
                Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                ClearColor = ClearColor(0f, 0f, 0f, 0f),
            },
        ];
        var renderArea = new VkRect2D { extent = new VkExtent2D { width = slot.Width, height = slot.Height } };
        rec.BeginRendering(new RenderingInfo { RenderArea = renderArea, LayerCount = 1, ColorAttachments = color });
        SetViewportAndScissor(ref rec, in renderArea);
        rec.BindPipeline(in overlay.UiPipeline);
        rec.Draw(6);
        rec.EndRendering();
    }

    /// <summary><c>Backbuffer = Ui + (1 − α)·Hudless</c> (DLSS-FG guide
    /// l. 742-754), on the raw encoded texels.</summary>
    private static void RecordComposite(ref CommandRecorder rec, FrameGenTargets slot, OverlayPipelines overlay)
    {
        ReadOnlySpan<ColorAttachment> color =
        [
            new ColorAttachment
            {
                View    = slot.BackbufferView,
                Layout  = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                LoadOp  = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_DONT_CARE,
                StoreOp = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
            },
        ];
        var renderArea = new VkRect2D { extent = new VkExtent2D { width = slot.Width, height = slot.Height } };
        rec.BeginRendering(new RenderingInfo { RenderArea = renderArea, LayerCount = 1, ColorAttachments = color });
        SetViewportAndScissor(ref rec, in renderArea);

        ReadOnlySpan<DescriptorWrite> writes =
        [
            DescriptorWrite.Image(
                binding: 0, arrayElement: 0, VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE,
                ImageDescriptorWrite.Of(in slot.HudlessView, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL)),
            DescriptorWrite.Image(
                binding: 1, arrayElement: 0, VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE,
                ImageDescriptorWrite.Of(in slot.UiView, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL)),
        ];
        rec.BindPipeline(in overlay.CompositePipeline);
        rec.PushDescriptorSet(VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS, in overlay.CompositeLayout, set: 0, writes);
        rec.Draw(3);
        rec.EndRendering();
    }

    /// <summary>
    /// <paramref name="source"/> (already in <c>TRANSFER_SRC_OPTIMAL</c>) into
    /// swapchain image <paramref name="imageIndex"/>, then to
    /// <c>PRESENT_SRC_KHR</c>.
    /// </summary>
    /// <remarks>
    /// The acquire barrier's source scope is <see cref="Stage.AllTransfer"/>,
    /// matching the stage the submit waits on the acquire semaphore at
    /// (docs/ngx-notes.md §6): a layout transition out of <c>UNDEFINED</c> is a
    /// write to the image and must not begin before the presentation engine is
    /// done with it. <c>Swapchain.GetImage</c>, not <c>GetImageHandle</c>,
    /// because the blit region reads the destination's extent.
    /// </remarks>
    private static void RecordPresentBlit(ref CommandRecorder rec, Swapchain swap, uint imageIndex, in Image source)
    {
        Image swapImage = swap.GetImage(imageIndex);
        rec.PipelineBarrier(ImageBarrier.Transition(
            in swapImage,
            VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
            Stage.AllTransfer, Access.None,
            Stage.AllTransfer, Access.TransferWrite));

        ReadOnlySpan<ImageBlitRegion> region = [ImageBlitRegion.WholeImage(in source, in swapImage)];
        rec.BlitImage(
            in source,    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
            in swapImage, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
            region, VkFilter.VK_FILTER_NEAREST);

        rec.PipelineBarrier(ImageBarrier.Transition(
            in swapImage,
            VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
            Stage.AllTransfer,  Access.TransferWrite,
            Stage.BottomOfPipe, Access.None));
    }

    private static void SetViewportAndScissor(ref CommandRecorder rec, in VkRect2D area)
    {
        // Positive-height viewport: the projection already flips Y.
        rec.SetViewport(new VkViewport
        {
            x = 0, y = 0, width = area.extent.width, height = area.extent.height, minDepth = 0, maxDepth = 1,
        });
        rec.SetScissor(area);
    }

    // ---------------------------------------------------------------------
    //  Camera.
    // ---------------------------------------------------------------------

    private const float FieldOfViewRadians = 40f * (MathF.PI / 180f);
    private const float NearPlane          = 0.1f;
    private const float FarPlane           = 100f;

    /// <summary>
    /// A STATIC cube under an ORBITING camera. The camera has to move for the
    /// dev DLL's "Clip to Previous Clip Matrix Test" to discriminate anything:
    /// with a fixed camera, ClipToPrevClip is the identity under either
    /// orientation (#253 PR C step 19a). <c>proj.M22 *= -1f</c> gives a y-down
    /// NDC and a [0,1] depth range, the HelloDlaa convention.
    /// </summary>
    private static void BuildMatrices(float seconds, float aspect, out Matrix4x4 model, out Matrix4x4 view, out Matrix4x4 proj)
    {
        // A fixed tilt so three faces are visible; the cube itself never moves.
        model = Matrix4x4.CreateRotationY(0.6f) * Matrix4x4.CreateRotationX(0.4f);

        float angle = seconds * 0.35f;
        var position = new Vector3(7.0f * MathF.Sin(angle), 1.5f, -7.0f * MathF.Cos(angle));
        view = Matrix4x4.CreateLookAt(position, Vector3.Zero, Vector3.UnitY);

        proj = Matrix4x4.CreatePerspectiveFieldOfView(FieldOfViewRadians, aspect, NearPlane, FarPlane);
        proj.M22 *= -1f;
    }

    /// <summary>
    /// The camera block, built from the same unjittered view and projection as
    /// the motion vectors. Position and basis come from the inverse view: for a
    /// System.Numerics (row-vector, right-handed) look-at, its rows are right,
    /// up, backward and position.
    /// </summary>
    private static DlssFrameGenerationCamera BuildCamera(
        in FrameGenOptions options,
        in Matrix4x4 proj, in Matrix4x4 clipToView,
        in Matrix4x4 clipToPrevClip, in Matrix4x4 prevClipToClip,
        in Matrix4x4 viewToWorld, float aspect)
    {
        // --transpose-matrices: the 19a "Run B" variant. Transposing here hands
        // NGX exactly the bytes EvaluateCore would hand it if it transposed.
        bool t = options.TransposeMatrices;
        return new DlssFrameGenerationCamera
        {
            ViewToClip     = t ? Matrix4x4.Transpose(proj)           : proj,
            ClipToView     = t ? Matrix4x4.Transpose(clipToView)     : clipToView,
            ClipToPrevClip = t ? Matrix4x4.Transpose(clipToPrevClip) : clipToPrevClip,
            PrevClipToClip = t ? Matrix4x4.Transpose(prevClipToClip) : prevClipToClip,
            Position    = new Vector3(viewToWorld.M41, viewToWorld.M42, viewToWorld.M43),
            Right       = new Vector3(viewToWorld.M11, viewToWorld.M12, viewToWorld.M13),
            Up          = new Vector3(viewToWorld.M21, viewToWorld.M22, viewToWorld.M23),
            Forward     = new Vector3(-viewToWorld.M31, -viewToWorld.M32, -viewToWorld.M33),
            Near        = NearPlane,
            Far         = FarPlane,
            FieldOfView = FieldOfViewRadians,
            AspectRatio = aspect,
        };
    }

    // ---------------------------------------------------------------------
    //  Setup and resize.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Called at start-up and after every swapchain recreate: per-slot
    /// targets, capture readbacks, and the Frame Generation feature, which
    /// must be recreated on any resize (DLSS-FG guide l. 657-668).
    /// </summary>
    private static void RebuildForExtent(
        Device device, Queue queue, CommandBufferPool pool, in FrameGenOptions options,
        VkExtent2D extent, bool fg, FrameGenTargets?[] targets, ref Buffer readbackGen, ref Buffer readbackReal)
    {
        // Resize is not per-frame: drain everything before freeing anything.
        device.WaitIdle();

        uint width = extent.width, height = extent.height;
        for (int i = 0; i < targets.Length; i++)
        {
            targets[i]?.Dispose();
            targets[i] = FrameGenTargets.Create(device, width, height);
        }

        readbackGen.Dispose();
        readbackReal.Dispose();
        readbackGen  = options.CaptureGenerated is not null && fg ? CreateReadback(device, width, height) : default;
        readbackReal = options.CaptureReal is not null ? CreateReadback(device, width, height) : default;

        s_fg?.Dispose();
        s_fg = null;
        if (fg && s_ngx is not null)
        {
            // Backbuffer and outputs at the swapchain extent; depth and motion
            // vectors at the same extent, because nothing here upscales.
            s_fgDescription = new DlssFrameGenerationDescription
            {
                Width            = width,
                Height           = height,
                BackbufferFormat = FrameGenTargets.ColorFormat,
                RenderWidth      = width,
                RenderHeight     = height,
            };

            // ImmediateSubmit submits AND WAITS, which CreateFrameGeneration
            // requires before the first evaluate.
            queue.ImmediateSubmit(pool, static (ref CommandRecorder recorder) =>
            {
                s_fg = s_ngx!.CreateFrameGeneration(ref recorder, in s_fgDescription);
            });
        }

        Console.WriteLine($"Targets {width}x{height}{(s_fg is null ? "" : ", Frame Generation feature created")}.");
    }

    private static Buffer CreateReadback(Device device, uint width, uint height)
        => device.Allocator.CreateBuffer(
            new BufferDescription { Size = (ulong)width * height * 4, Usage = BufferUsage.TransferDst },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

    /// <summary>
    /// Drains the device, rebuilds the swapchain at the window's size with the
    /// same <c>LowLatency</c> value, and rotates every acquire semaphore a
    /// failed frame left signaled: the ring's own and the sample's extra ones.
    /// </summary>
    /// <remarks>
    /// <c>device.WaitIdle</c>, not only <c>ring.WaitForInFlightFences</c>: a
    /// frame abandoned after its first submit leaves that submit with no fence
    /// (rec1 is fenceless by design, and rec2 never ran), so the ring's fences
    /// do not cover it.
    /// </remarks>
    private static bool TryRecreate(
        Device device, Swapchain swap, FrameRing ring, SemaphorePool semaphores,
        BinarySemaphore[] extraAcquire, bool[] extraPending,
        in Surface surface, SdlWindow window, ReadOnlySpan<VkSurfaceFormatKHR> preferredFormats,
        in FrameGenOptions options)
    {
        device.WaitIdle();
        SwapchainState state;
        try
        {
            state = swap.Recreate(new SwapchainDescription
            {
                Surface              = surface,
                Width                = window.Width,
                Height               = window.Height,
                PreferredFormats     = preferredFormats,
                PreferredPresentMode = options.PresentMode,
                ImageUsage           = ImageUsage.ColorAttachment | ImageUsage.TransferDst,
                LowLatency           = device.IsNvLowLatency2Enabled,
            }, ring.WaitForInFlightFences);
        }
        catch (VulkanException e) when (e.Result is VkResult.VK_ERROR_SURFACE_LOST_KHR)
        {
            return false;
        }

        ring.RecycleStaleAcquireSemaphores();

        // An extra acquire that returned Success/Suboptimal and was not
        // consumed by rec1's submit is host-signaled forever: Vulkan has no
        // host reset for binary semaphores, so destroy and replace it.
        for (int i = 0; i < extraAcquire.Length; i++)
        {
            if (!extraPending[i]) continue;
            semaphores.Discard(extraAcquire[i]);
            extraAcquire[i] = semaphores.AcquireBinary();
            extraPending[i] = false;
        }

        return state == SwapchainState.Ready;
    }

    // ---------------------------------------------------------------------
    //  NGX setup helpers.
    // ---------------------------------------------------------------------

    private static Utf8Name[] QueryInstanceExtensionUnion(in NgxDescription description, out bool frameGenerationOk)
    {
        Utf8Name[] result = [];
        frameGenerationOk = false;
        if (NgxSupport.TryGetInstanceExtensions(NgxFeature.FrameGeneration, in description, out NgxExtensionSet? fg))
        {
            using (fg) result = Union(result, fg.Names);
            frameGenerationOk = true;
        }
        if (NgxSupport.TryGetInstanceExtensions(NgxFeature.SuperResolution, in description, out NgxExtensionSet? sr))
        {
            using (sr) result = Union(result, sr.Names);
        }
        return result;
    }

    private static Utf8Name[] QueryDeviceExtensionUnion(PhysicalDevice gpu, in NgxDescription description, out bool frameGenerationOk)
    {
        Utf8Name[] result = [];
        frameGenerationOk = false;
        if (NgxSupport.TryGetDeviceExtensions(NgxFeature.FrameGeneration, gpu, in description, out NgxExtensionSet? fg))
        {
            using (fg) result = Union(result, fg.Names);
            frameGenerationOk = true;
        }
        if (NgxSupport.TryGetDeviceExtensions(NgxFeature.SuperResolution, gpu, in description, out NgxExtensionSet? sr))
        {
            using (sr) result = Union(result, sr.Names);
        }
        return result;
    }

    /// <summary>
    /// <paramref name="first"/> plus every name of <paramref name="second"/>
    /// not already present, as a new array that owns no native memory.
    /// </summary>
    /// <remarks>
    /// An <see cref="NgxExtensionSet"/>'s names point into native memory it
    /// frees on dispose, so each name is copied into a pinned, process-lifetime
    /// UTF-8 buffer here (a handful of setup-time allocations).
    /// </remarks>
    private static Utf8Name[] Union(Utf8Name[] first, ReadOnlySpan<Utf8Name> second)
    {
        var all = new List<Utf8Name>(first.Length + second.Length);
        all.AddRange(first);
        for (int i = 0; i < second.Length; i++)
        {
            ReadOnlySpan<byte> name = MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)second[i].Ptr);
            bool present = false;
            for (int j = 0; j < all.Count; j++)
            {
                if (MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)all[j].Ptr).SequenceEqual(name))
                {
                    present = true;
                    break;
                }
            }
            if (present) continue;

            byte[] copy = GC.AllocateArray<byte>(name.Length + 1, pinned: true);
            name.CopyTo(copy);
            fixed (byte* p = copy) all.Add(new Utf8Name((sbyte*)p));
        }
        return all.ToArray();
    }

    /// <summary>
    /// A developer-machine convenience for <b>this repository</b>: walk up from
    /// the binary until a directory contains <c>native/ngx</c>, then hand NGX
    /// the staged <c>rel/</c> folder.
    /// </summary>
    /// <remarks>
    /// Measured on driver 616.92 / NGX v310.9.1: when this folder and a copy
    /// beside the executable both hold <c>nvngx_dlssg.dll</c>, NGX loads the
    /// one from this folder. To run the <c>dev/</c> build (the visualizer),
    /// run a copy of the build directory from outside the repository, with the
    /// dev DLL beside it (docs/ngx-frame-generation-notes.md §1).
    /// </remarks>
    private static string[] BuildSearchPaths()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "native", "ngx")))
            directory = directory.Parent;

        if (directory is null) return [];

        string rid    = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";
        string staged = Path.Combine(directory.FullName, "native", "ngx", "staged", rid, "rel");
        return Directory.Exists(staged) ? [staged] : [];
    }

    /// <summary>
    /// Frame Generation turned out to be unavailable. Returns
    /// <see langword="true"/> when the run carries on as <c>--mode off</c>;
    /// otherwise <paramref name="exitCode"/> is 5 (<c>--require-fg</c>).
    /// </summary>
    private static bool TryContinueWithoutFg(in FrameGenOptions options, string? reason, out int exitCode)
    {
        if (reason is not null) Console.Error.WriteLine(reason);

        if (options.RequireFg)
        {
            Console.Error.WriteLine("--require-fg was passed, so this is a failure rather than a fallback.");
            exitCode = 5;
            return false;
        }

        Console.WriteLine("Continuing as --mode off: the same frame, one present per rendered frame.");
        exitCode = 0;
        return true;
    }

    // ---------------------------------------------------------------------
    //  Measurement output (#253 PR C step 19).
    // ---------------------------------------------------------------------

    private static void LogPresent(long[] log, ref int count)
    {
        if (count < log.Length) log[count++] = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// The step 19d host-timestamp fallback: intervals between successive
    /// <c>Present</c> returns. Under fg they alternate generated → real and
    /// real → next generated, so both halves are reported separately. A host
    /// timestamp is when the call returned, not when the image was displayed;
    /// PresentMon's <c>MsBetweenDisplayChange</c> is the primary source.
    /// </summary>
    private static void ReportPresentIntervals(long[] log, int count, bool fg)
    {
        if (count < 3) return;
        var all = new List<double>(count);
        var even = new List<double>(count / 2 + 1);   // present 2k -> 2k+1 (generated -> real)
        var odd  = new List<double>(count / 2 + 1);   // present 2k+1 -> 2k+2 (real -> next generated)
        // Skip the first 10% as warm-up.
        int start = Math.Max(1, count / 10);
        for (int i = start; i < count; i++)
        {
            double ms = (log[i] - log[i - 1]) * 1000.0 / Stopwatch.Frequency;
            all.Add(ms);
            if (((i - 1) & 1) == 0) even.Add(ms); else odd.Add(ms);
        }
        Console.WriteLine($"Host present-return intervals ({all.Count} after warm-up): {Describe(all)}");
        if (fg)
        {
            Console.WriteLine($"  generated -> real: {Describe(even)}");
            Console.WriteLine($"  real -> next generated: {Describe(odd)}");
        }

        static string Describe(List<double> values)
        {
            if (values.Count == 0) return "n/a";
            double sum = 0;
            for (int i = 0; i < values.Count; i++) sum += values[i];
            values.Sort();
            double p50 = values[values.Count / 2];
            double p99 = values[Math.Min(values.Count - 1, (int)(values.Count * 0.99))];
            return $"mean {sum / values.Count:F3} ms, p50 {p50:F3} ms, p99 {p99:F3} ms, max {values[^1]:F3} ms";
        }
    }

    /// <summary>
    /// Host-side accumulators for one <c>--latency-report</c> window: where the
    /// CPU waits. A plain struct of Stopwatch ticks, so filling it allocates
    /// nothing (#253 plan step 19j).
    /// </summary>
    private struct ReportWindow
    {
        public long  StartTicks;
        public ulong PresentsAtStart;
        public ulong Frames;
        public long  SleepTicks;
        public long  BeginFrameTicks;
        public long  Acquire0Ticks;
        public long  Acquire1Ticks;
        public ulong Acquire1Count;
        public long  Present0Ticks;
        public long  Present1Ticks;
    }

    /// <summary>
    /// The <c>--reflex-interval-us</c> value: literal microseconds, or
    /// <c>frame</c> = <c>ceil(M × 1 000 000 / refreshHz)</c> with M = 2 under
    /// Frame Generation and 1 without, refreshHz from SDL3's current display
    /// mode (#253 plan step 17). Without the option: <c>frame</c> under Frame
    /// Generation over FIFO, otherwise 0 (no minimum interval);
    /// <c>--reflex-interval-us 0</c> turns the default off.
    /// </summary>
    /// <remarks>
    /// The FG + FIFO default is the #253 step 19j decision (spec F5, OPEN-9).
    /// Without an interval the LL2 sleep does not hold the CPU, so the loop
    /// runs until the frame fence blocks it with the present queue full: about
    /// 99 ms simulation to GPU end, 66 ms of it queued. An interval of one
    /// rendered frame (two refreshes) keeps 60 displayed fps and brought that to
    /// 17-28 ms borderless, but it only holds the queue at its startup depth;
    /// windowed stayed at 45-83 ms. One frame + 500 us drains the queue
    /// (11-17 ms) at the cost of a repeated frame about every 1.3 s. Draining
    /// it without repeats needs a pacer, which is follow-up work (#266).
    /// </remarks>
    private static uint ResolveSleepInterval(SdlWindow window, in FrameGenOptions options, bool fg)
    {
        bool fifo = options.PresentMode == VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR;
        if (!options.ReflexIntervalGiven && !(fg && fifo)) return 0;
        if (options.ReflexIntervalGiven && !options.ReflexIntervalFrame)
        {
            Console.WriteLine($"Reflex minimum interval: {options.ReflexIntervalUs} us (literal).");
            return options.ReflexIntervalUs;
        }

        double refreshHz = DisplayRefreshHz(window);
        uint presentsPerFrame = fg ? 2u : 1u;
        if (refreshHz <= 0)
        {
            Console.Error.WriteLine("Reflex minimum interval: the display reports no refresh rate; using 0.");
            return 0;
        }
        uint us = (uint)Math.Ceiling(presentsPerFrame * 1_000_000.0 / refreshHz);
        Console.WriteLine($"Reflex minimum interval: {us} us = ceil({presentsPerFrame} x 1e6 / {refreshHz:F3} Hz) ('frame').");
        return us;
    }

    private static double DisplayRefreshHz(SdlWindow window)
    {
        var display = SDL.SDL3.SDL_GetDisplayForWindow((SDL.SDL_Window*)window.Handle);
        SDL.SDL_DisplayMode* mode = SDL.SDL3.SDL_GetCurrentDisplayMode(display);
        if (mode == null) return 0;
        // The rational form is exact (59.951 Hz, not 60); fall back to the float.
        if (mode->refresh_rate_numerator != 0 && mode->refresh_rate_denominator != 0)
            return (double)mode->refresh_rate_numerator / mode->refresh_rate_denominator;
        return mode->refresh_rate;
    }

    /// <summary>
    /// The <c>--latency-report</c> line (#253 PR C step 19i), modelled on
    /// HelloCube's: mean simulation-start → GPU-render-end over the driver's
    /// populated reports, plus the #261 OPEN-3 evidence: how many reports share
    /// a frame id (must be 0 under two presents per frame), whether the
    /// present timestamps are populated, and the frame rate the reports imply
    /// (must equal the rendered rate, not the present rate).
    /// </summary>
    private static void ReportLatency(
        Swapchain swap, Span<LatencyTimings> timings, ulong presents, in ReportWindow window, ulong sleepTimeouts)
    {
        double seconds = (Stopwatch.GetTimestamp() - window.StartTicks) / (double)Stopwatch.Frequency;
        double frames  = Math.Max(1, window.Frames);
        double toMs    = 1000.0 / Stopwatch.Frequency;
        string sleepMs = (window.SleepTicks * toMs / frames).ToString("F2");
        string fenceMs = (window.BeginFrameTicks * toMs / frames).ToString("F2");
        string acq0Ms  = (window.Acquire0Ticks * toMs / frames).ToString("F2");
        string acq1Ms  = window.Acquire1Count == 0 ? "n/a" : (window.Acquire1Ticks * toMs / window.Acquire1Count).ToString("F2");
        ulong renderedFrames = window.Frames;
        presents -= window.PresentsAtStart;
        string pres0Ms = (window.Present0Ticks * toMs / frames).ToString("F2");
        string pres1Ms = window.Acquire1Count == 0 ? "n/a" : (window.Present1Ticks * toMs / window.Acquire1Count).ToString("F2");
        string cpuWait = $"cpu wait per frame: sleep {sleepMs} ms, frame fence {fenceMs} ms, acquire0 {acq0Ms} ms, acquire1 {acq1Ms} ms, " +
                         $"present0 {pres0Ms} ms, present1 {pres1Ms} ms; ";
        int n = swap.GetLatencyTimings(timings);
        int populated = 0, duplicates = 0, nonIncreasing = 0, presentMissing = 0;
        double sum = 0, osQueueSum = 0, presentEndSum = 0;
        ulong firstId = 0, lastId = 0, firstSim = 0, lastSim = 0, previousId = 0;
        for (int i = 0; i < n; i++)
        {
            ref readonly LatencyTimings t = ref timings[i];
            if (t.FrameId == 0) continue;
            if (previousId != 0)
            {
                if (t.FrameId == previousId) duplicates++;
                else if (t.FrameId < previousId) nonIncreasing++;
            }
            previousId = t.FrameId;
            if (t.PresentStartTimeUs == 0 || t.PresentEndTimeUs == 0) presentMissing++;
            if (t.SimulationStartTimeUs == 0 || t.GpuRenderEndTimeUs < t.SimulationStartTimeUs) continue;
            populated++;
            sum += t.GpuRenderEndTimeUs - t.SimulationStartTimeUs;
            // Where FIFO back-pressure shows up: submitted work waiting in the
            // OS render queue before the GPU starts it (docs/reflex-notes.md §7.4).
            if (t.OsRenderQueueStartTimeUs != 0 && t.GpuRenderStartTimeUs >= t.OsRenderQueueStartTimeUs)
                osQueueSum += t.GpuRenderStartTimeUs - t.OsRenderQueueStartTimeUs;
            if (t.PresentEndTimeUs >= t.SimulationStartTimeUs)
                presentEndSum += t.PresentEndTimeUs - t.SimulationStartTimeUs;
            if (firstId == 0) { firstId = t.FrameId; firstSim = t.SimulationStartTimeUs; }
            lastId = t.FrameId; lastSim = t.SimulationStartTimeUs;
        }
        string mean = populated == 0 ? "n/a" : (sum / populated / 1000.0).ToString("F2") + " ms";
        string osq  = populated == 0 ? "n/a" : (osQueueSum / populated / 1000.0).ToString("F2") + " ms";
        string pend = populated == 0 ? "n/a" : (presentEndSum / populated / 1000.0).ToString("F2") + " ms";
        string timingsFps = lastSim > firstSim
            ? ((populated - 1) * 1e6 / (lastSim - firstSim)).ToString("F2")
            : "n/a";
        Console.WriteLine(
            $"[latency] window of {renderedFrames} frames: rendered {renderedFrames / seconds:F2} fps, presents {presents / seconds:F2}/s; " +
            cpuWait +
            $"sim->gpu-end mean {mean} (os-queue wait {osq}, sim->present-end {pend}) over {populated}/{n} reports " +
            $"(ids {firstId}..{lastId}); " +
            $"duplicate ids {duplicates}, out-of-order {nonIncreasing}, present stamps missing {presentMissing}; " +
            $"timings fps {timingsFps}; sleep timeouts {sleepTimeouts}; pcl pings seen {SdlWindow.PclPingsSeen}");
    }

    private static void WriteCaptures(
        in FrameGenOptions options, bool captured, bool fg, Buffer readbackGen, Buffer readbackReal, FrameGenTargets? any)
    {
        if (!options.Captures) return;
        if (!captured || any is null)
        {
            Console.Error.WriteLine(
                $"No capture written: the run ended before frame {options.MaxFrames}, so the readback copy never ran.");
            return;
        }

        int w = (int)any.Width, h = (int)any.Height;
        if (options.CaptureGenerated is not null)
        {
            if (!fg || readbackGen.IsNull)
            {
                Console.Error.WriteLine("--capture-generated ignored: Frame Generation is not running.");
            }
            else
            {
                readbackGen.Invalidate();
                PngWriter.Write(options.CaptureGenerated, readbackGen.AsReadOnlySpan<byte>(), w, h);
                Console.WriteLine($"Wrote {options.CaptureGenerated} ({w}x{h}) from OutputInterpolated.");
            }
        }
        if (options.CaptureReal is not null && !readbackReal.IsNull)
        {
            readbackReal.Invalidate();
            PngWriter.Write(options.CaptureReal, readbackReal.AsReadOnlySpan<byte>(), w, h);
            Console.WriteLine($"Wrote {options.CaptureReal} ({w}x{h}) from {(fg ? "OutputReal" : "Backbuffer")}.");
        }
    }

    // ---------------------------------------------------------------------
    //  Misc.
    // ---------------------------------------------------------------------

    private static void ReportSurfaceLost()
    {
        Console.Error.WriteLine(
            "The window surface was lost (VK_ERROR_SURFACE_LOST_KHR — typically a display-driver " +
            "restart, a session switch or a monitor change). A swapchain over a lost surface cannot " +
            "be recreated, so this sample exits rather than retrying.");
    }

    private static void OnValidationMessage(DebugMessage msg)
    {
        bool isError = (msg.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0;
        bool isWarn  = (msg.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT) != 0;
        if (isError) System.Threading.Interlocked.Increment(ref s_validationErrors);
        if (isWarn)  System.Threading.Interlocked.Increment(ref s_validationWarnings);
        if (!isError && !isWarn) return;
        string tag = isError ? "ERROR" : "WARN ";
        Console.Error.WriteLine($"[VK {tag}] {msg.MessageIdName ?? "?"}: {msg.Message}");
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
}
