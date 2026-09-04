using System.Diagnostics;
using System.Numerics;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Ngx;
using Ahjo.Vulkan.Utilities;

namespace Ahjo.Vulkan.Samples.HelloDlaa;

/// <summary>
/// A jittered, spinning, high-frequency-textured cube run through NVIDIA DLSS
/// — DLAA at native resolution, or DLSS Quality upscaling from a smaller render
/// extent — with the two comparison controls (<c>off</c>, <c>bilinear</c>) that
/// make the reconstruction judgeable.
/// </summary>
/// <remarks>
/// <para>This sample exists because the wrapper cannot hold the renderer's half
/// of the DLSS contract (#218 D4). Jitter, motion vectors, mip bias and image
/// layouts are the caller's, every one of them fails silently, and the only
/// place they can be <i>shown</i> is a real frame. The consumer-facing writeup
/// is <c>docs/ngx-notes.md</c>; this file is the worked call site it points
/// at.</para>
/// <para><b>Needs an NVIDIA GPU with a DLSS-capable driver and a
/// consumer-supplied <c>nvngx_dlss.dll</c></b> (#214). CI builds this sample and
/// never runs it — there is no NVIDIA hardware in CI (#32). Without the feature
/// DLL it prints the wrapper's diagnosis and exits 0, or 5 under
/// <c>--require-dlss</c>.</para>
/// <para><b>The frame loop allocates nothing.</b> No <c>T[]</c>, no lambdas, no
/// LINQ, no interpolated strings, no per-frame console output — spec E12. The
/// existing windowed samples do allocate in their loops
/// (<c>HelloVmaWindowed/Program.cs:366</c>); this one deliberately does not.</para>
/// <para>That holds on the <b>steady-state</b> path — the one where
/// <c>AcquireNextImage</c> returns <see cref="AcquireResult.Success"/>. The
/// <see cref="AcquireResult.OutOfDate"/> / <see cref="AcquireResult.Suboptimal"/>
/// branches route into <c>RebuildForExtent</c>, which allocates freely (a new
/// <c>JitterSequence</c>, new <c>FrameTargets</c>, a new <c>DlssFeature</c>, an
/// interpolated status line) because it is a resize path, not a frame path. A
/// driver or compositor that returned <c>VK_SUBOPTIMAL_KHR</c> <i>persistently</i>
/// rather than converging would turn this into an allocating loop. That does not
/// happen on the hardware this was measured on; it is not an unconditional
/// property of the code.</para>
/// </remarks>
internal static unsafe class Program
{
    private const uint FramesInFlight = 2;

    /// <summary>
    /// How long to sleep between retries while the window is minimized and the
    /// swapchain has no presentable extent. A real application blocks on its
    /// event queue instead; a sample that spun here would peg a core.
    /// </summary>
    private const int MinimizedPollMilliseconds = 16;

    // Validation messages fire from arbitrary driver threads — static counters
    // keep the callback closure captureless.
    private static int s_validationErrors;
    private static int s_validationWarnings;

    // ---- Per-extent state, rebuilt at start-up and on every resize. Static
    // so the CreateDlss recorder lambda below can stay `static` and capture
    // nothing; this is setup-path state, never touched from the frame loop
    // except by reading.
    private static NgxContext?  s_ngx;
    private static DlssFeature? s_dlss;
    private static DlssFeatureDescription s_dlssDescription;

    private static int Main(string[] args)
    {
        // ---- 1. Command line. -------------------------------------------
        if (!DlaaOptions.TryParse(args, out DlaaOptions options, out string? parseError))
        {
            Console.Error.WriteLine(parseError);
            return 2;
        }

        // ---- 2. Wrapper validation, BEFORE any handle exists. ------------
        // The double-dispose registry only tracks handles created while this
        // is on, so flipping it later leaves earlier handles untracked. It
        // costs the frame loop nothing: a disabled check is one branch, and an
        // enabled one only builds its message on the failure path (spec E13).
        AhjoValidation.Enabled = options.Validation;

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "cube.slang");
        if (!File.Exists(shaderPath))
        {
            Console.Error.WriteLine($"Missing shader. Expected: {shaderPath}");
            return 2;
        }

        // ---- 3. The NGX description. -------------------------------------
        var ngxDescription = new NgxDescription
        {
            ProjectId     = "5f3b2c41-9a6d-4b18-8e77-2c0d5a9b7e14",  // this sample's own, fixed
            EngineVersion = "0.1.0-hellodlaa",
            // ApplicationDataPath deliberately unset: the wrapper materializes
            // Path.GetTempPath() itself, and a null reaching NGX
            // access-violates the process (NgxDescription.cs:36-47).
            DlssSearchPaths = BuildSearchPaths(),
            // LoggingLevel stays Off: NGX logging above Off allocates per
            // callback by design (src/Ahjo.Vulkan.Ngx/CLAUDE.md), and this
            // sample holds a zero-allocation loop.
        };

        // ---- 4. Window. ---------------------------------------------------
        using var window = new SdlWindow("Ahjo.Vulkan — HelloDlaa (Esc to quit)", 1600, 900,
            hidden: false, resizable: true);

        // ---- 5. NGX instance extensions. ----------------------------------
        // The order of 5→11 is the contract, stated on NgxSupport: instance
        // extensions → instance → physical device → device extensions →
        // device → NgxContext.Create. BOTH lists are mandatory and they fail
        // differently: missing INSTANCE extensions access-violate inside
        // NVIDIA's client library (no managed catch recovers); missing DEVICE
        // extensions let Init succeed and then report Available = 0 with
        // FAIL_PlatformError, which reads exactly like an unsupported GPU.
        //
        // `off` never touches NGX at all. `bilinear` does, even though it never
        // evaluates: it asks GetOptimalSettings for the extent `quality` would
        // use, so the control renders exactly the same pixels. When NGX is not
        // available, bilinear falls back to the guide's ratio and runs anyway —
        // a control mode that needs a proprietary DLL to run is not a control.
        bool wantsNgx = options.Mode != DlaaMode.Off;

        NgxExtensionSet? ngxInstanceExts = null;
        if (wantsNgx)
        {
            try
            {
                if (!NgxSupport.TryGetInstanceExtensions(in ngxDescription, out ngxInstanceExts)
                    && !TryContinueWithoutNgx(in options, "NGX could not report the instance extensions DLSS requires.", out int code))
                {
                    return code;
                }
                wantsNgx = ngxInstanceExts is not null;
            }
            catch (DllNotFoundException)
            {
                // On a clone with no NGX SDK staged there is no ahjo_ngx shim
                // and the P/Invoke throws. That is the clean-skip path.
                if (!TryContinueWithoutNgx(in options,
                        "The ahjo_ngx shim is not present. Run ./tools/setup-ngx.ps1 to stage the NGX SDK and build it.",
                        out int code))
                {
                    return code;
                }
                wantsNgx = false;
            }
        }

        Instance instance;
        try
        {
            Utf8Name[] instanceExtensions = ConcatExtensions(
                SdlWindow.GetRequiredVulkanInstanceExtensions(),
                ngxInstanceExts is null ? default : ngxInstanceExts.Names);

            // ---- 6. Instance. --------------------------------------------
            instance = Instance.Create(new InstanceDescription
            {
                Extensions       = instanceExtensions,
                EnableValidation = options.Validation,
                DebugCallback    = options.Validation ? OnValidationMessage : null,
            });
        }
        finally
        {
            // vkCreateInstance copied the names; the set can go now.
            ngxInstanceExts?.Dispose();
        }

        using (instance)
        {
            using var surface = window.CreateVulkanSurface(instance);

            // ---- 7. Physical device: graphics + present. -----------------
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

            // ---- 8. NGX device extensions. -------------------------------
            NgxExtensionSet? ngxDeviceExts = null;
            if (wantsNgx && !NgxSupport.TryGetDeviceExtensions(gpu, in ngxDescription, out ngxDeviceExts))
            {
                if (!TryContinueWithoutNgx(in options,
                        "NGX could not report the device extensions DLSS requires on this GPU.", out int code))
                {
                    return code;
                }
                wantsNgx = false;
            }

            // ---- 9. Device. ----------------------------------------------
            // VK_EXT_memory_budget and AllocatorDescription.EnableMemoryBudget
            // are BOTH or NEITHER — the wrapper fails the pairing check when
            // the flag is set without the extension (#218 D11).
            bool memoryBudget = gpu.SupportsExtension(VulkanExtensions.ExtMemoryBudget);

            Device device;
            try
            {
                Utf8Name[] deviceExtensions = BuildDeviceExtensions(
                    ngxDeviceExts is null ? default : ngxDeviceExts.Names, memoryBudget);

                device = gpu.CreateDevice(new DeviceDescription
                {
                    Queues     = [new QueueRequest(family, count: 1, priority: 1.0f)],
                    Extensions = deviceExtensions,
                    Allocator  = new AllocatorDescription { EnableMemoryBudget = memoryBudget },
                });
            }
            finally
            {
                ngxDeviceExts?.Dispose();   // vkCreateDevice copied the names
            }

            using (device)
            {
                return Run(window, in surface, device, family, in options, in ngxDescription,
                           shaderPath, memoryBudget, wantsNgx);
            }
        }
    }

    private static int Run(
        SdlWindow window,
        in Surface surface,
        Device device,
        uint family,
        in DlaaOptions options,
        in NgxDescription ngxDescription,
        string shaderPath,
        bool memoryBudget,
        bool wantsNgx)
    {
        var queue = device.GetQueue(family, 0);

        // ---- 10. Swapchain. ----------------------------------------------
        // UNORM preferred, not sRGB: the fragment shader encodes sRGB itself
        // (spec D4) and blitting an already-encoded UNORM source into an _SRGB
        // destination encodes a second time.
        ReadOnlySpan<VkSurfaceFormatKHR> preferred =
        [
            new() { format = VkFormat.VK_FORMAT_B8G8R8A8_UNORM, colorSpace = VkColorSpaceKHR.VK_COLOR_SPACE_SRGB_NONLINEAR_KHR },
            new() { format = VkFormat.VK_FORMAT_R8G8B8A8_UNORM, colorSpace = VkColorSpaceKHR.VK_COLOR_SPACE_SRGB_NONLINEAR_KHR },
        ];
        //
        // TransferDst on the swapchain is what makes the present blit legal.
        // The spec only guarantees COLOR_ATTACHMENT in
        // VkSurfaceCapabilitiesKHR.supportedUsageFlags, and Swapchain forwards
        // the requested usage with no clamp, so a surface that does not
        // advertise TRANSFER_DST fails creation
        // (VUID-VkSwapchainCreateInfoKHR-imageUsage-01276) rather than
        // downgrading. Every mainstream desktop driver advertises it; check
        // before copying this into something that must run anywhere.
        var swapDesc = new SwapchainDescription
        {
            Surface          = surface,
            Width            = window.Width,
            Height           = window.Height,
            PreferredFormats = preferred,
            ImageUsage       = ImageUsage.ColorAttachment | ImageUsage.TransferDst,
        };
        using var swap = new Swapchain(device, in swapDesc);

        Console.WriteLine($"Swapchain: {swap.Format} {swap.Extent.width}x{swap.Extent.height}, " +
            $"{swap.ImageCount} images, {swap.PresentMode}");
        if (IsSrgbFormat(swap.Format))
        {
            Console.WriteLine("WARNING: the surface only offers an _SRGB swapchain format. The shader already " +
                "encodes sRGB, so the presentation blit will encode a second time and the image will look " +
                "washed out. This is a property of the surface, not a bug in the sample (spec D4).");
        }

        // ---- 11. NGX context. ---------------------------------------------
        if (wantsNgx)
        {
            try
            {
                s_ngx = NgxContext.Create(device, in ngxDescription);
            }
            catch (NgxFeatureLibraryNotFoundException ex)
            {
                // Printed VERBATIM: the message already names the file and
                // every directory searched. That diagnosis is the reason the
                // wrapper throws a typed exception at all — do not reformat it.
                Console.Error.WriteLine(ex.Message);
                if (!TryContinueWithoutNgx(in options, null, out int code)) return code;
                s_ngx = null;
            }
            catch (NgxDriverTooOldException ex)
            {
                Console.Error.WriteLine(ex.Message);
                if (!TryContinueWithoutNgx(in options, null, out int code)) return code;
                s_ngx = null;
            }

            // ---- 12. DLSS actually offered? ------------------------------
            if (s_ngx is not null && !s_ngx.IsSuperSamplingAvailable)
            {
                s_ngx.Dispose();
                s_ngx = null;
                if (!TryContinueWithoutNgx(in options,
                        "NGX reports DLSS Super Resolution is not available on this device.", out int code))
                {
                    return code;
                }
            }
        }

        using var scene    = new CubeScene(device, family, FramesInFlight);
        using var pipeline = new CubePipeline(device, shaderPath,
            FrameTargets.ColorFormat, FrameTargets.MotionFormat, FrameTargets.DepthFormat);

        using var ring        = new FrameRing(device, framesInFlight: FramesInFlight, queueFamily: family);
        using var setupPool   = new CommandBufferPool(device, family);

        var targets  = new FrameTargets?[FramesInFlight];
        Buffer readback = default;
        JitterSequence? jitter = null;
        uint renderW = 0, renderH = 0, outW = 0, outH = 0;
        DlaaMode effectiveMode = options.Mode;
        bool resetNextFrame = true;

        int exitCode = 0;
        var clock = Stopwatch.StartNew();
        ulong frame = 0;

        try
        {
            // Inside the try, not beside the CubePipeline construction: every
            // exit from here on has to run the finally below, because
            // NgxContext must be disposed before the Device it was created on.
            if (pipeline.Failed) return 2;

            if (!RebuildForExtent(device, queue, setupPool, scene, in options, swap.Extent, memoryBudget,
                                  targets, ref readback, ref jitter,
                                  ref renderW, ref renderH, ref outW, ref outH, ref effectiveMode))
            {
                return 5;
            }

            // Hoisted out of the loop so no per-frame T[] is needed; the span
            // form of BindVertexBuffers is what makes that possible.
            var previousUnjitteredMvp = Matrix4x4.Identity;
            bool havePrevious = false;
            // The readback buffer is host-visible VMA memory and is NOT
            // zero-initialized. Only the CopyImageToBuffer below ever fills it,
            // and that runs on one frame -- so any early exit (Esc, a close, an
            // OutOfDate bail on the final frame) would otherwise write a PNG
            // full of whatever bytes VMA handed back. No Vulkan rule is broken;
            // the artefact is just silently garbage, which is worse.
            bool captured = false;
            // Set by EVERY path that recreates the swapchain, including the
            // OutOfDate/Suboptimal ones -- and that is the point. Those change
            // the extent without an SDL resize event, so testing window.Width
            // against swap.Extent on the next iteration would report "no
            // change" and leave every per-extent resource sized for the old
            // one. The present blit would then silently rescale.
            bool rebuildPending = false;

            while (!window.ShouldClose)
            {
                window.PumpEvents();
                if (window.ShouldClose) break;

                // Terminal first (#222): a lost surface can never be recovered
                // by Recreate, so this must be tested BEFORE the "not Ready ->
                // recreate" guard below, which would otherwise retry over it.
                // All three observation points funnel here rather than each
                // carrying its own report-and-break: acquire and present return
                // AcquireResult.SurfaceLost, and Recreate absorbs a
                // VK_ERROR_SURFACE_LOST_KHR from its own surface query and
                // returns false (#222). DeviceLost is deliberately absent:
                // device loss throws out of acquire, present and Recreate
                // alike, so a sample never observes the state and a branch for
                // it would be unreachable.
                if (swap.State is SwapchainState.SurfaceLost)
                {
                    ReportSurfaceLost();
                    break;
                }

                // Consumed into a local BEFORE the || chain: inside it, a
                // short-circuit on an earlier term would swallow the event and
                // leave _resized set for a frame that no longer needs it.
                bool resized = window.ConsumeResize();

                // swap.State is the authority on presentability now (#222) -- the
                // sample no longer mirrors it in a sticky local. It is the FIRST
                // term, so a Minimized or NeedsRecreate loop keeps re-entering the
                // recreate path instead of falling through: after a minimize,
                // ConsumeResize() is false and swap.Extent still equals the window
                // size (Recreate returns Minimized BEFORE assigning its extent
                // field), so every other term below goes false.
                if (swap.State != SwapchainState.Ready || resized ||
                    swap.Extent.width != window.Width || swap.Extent.height != window.Height)
                {
                    if (!TryRecreate(device, swap, ring, in surface, window, preferred))
                    {
                        // Still minimized. Sleep rather than spin; a real
                        // application blocks on the event queue instead.
                        System.Threading.Thread.Sleep(MinimizedPollMilliseconds);
                        continue;
                    }
                    rebuildPending = true;
                }

                if (rebuildPending)
                {
                    if (!RebuildForExtent(device, queue, setupPool, scene, in options, swap.Extent, memoryBudget,
                                          targets, ref readback, ref jitter,
                                          ref renderW, ref renderH, ref outW, ref outH, ref effectiveMode))
                    {
                        return 5;
                    }
                    rebuildPending = false;
                    resetNextFrame = true;
                    havePrevious   = false;
                }

                using var fc = ring.BeginFrame();

                var acq = swap.AcquireNextImage(fc.ImageAcquired, TimeSpan.FromSeconds(1), out uint imageIndex);
                if (acq is AcquireResult.Success or AcquireResult.Suboptimal)
                    fc.MarkImageAcquireSignaled();
                if (acq is AcquireResult.OutOfDate or AcquireResult.Suboptimal)
                {
                    if (TryRecreate(device, swap, ring, in surface, window, preferred))
                        rebuildPending = true;
                    else
                        System.Threading.Thread.Sleep(MinimizedPollMilliseconds);
                    continue;
                }
                // Everything left is Timeout, NotReady or SurfaceLost. The
                // first two touch no swapchain state and acquired neither an
                // image nor a signalled semaphore, so retrying next iteration
                // is correct and there is nothing to recycle. SurfaceLost has
                // already moved the swapchain to SwapchainState.SurfaceLost,
                // which the loop-top guard catches on the next iteration.
                if (acq != AcquireResult.Success) continue;

                FrameTargets slot = targets[fc.SlotIndex]!;

                // ---- Matrices. -------------------------------------------
                float seconds = (float)clock.Elapsed.TotalSeconds;
                float aspect  = renderH == 0 ? 1f : renderW / (float)renderH;
                BuildMatrices(seconds, aspect, out Matrix4x4 model, out Matrix4x4 unjitteredMvp);

                // First frame after a rebuild: previous == current, so the
                // motion vectors are zero rather than garbage. Paired with
                // Reset = true below.
                if (!havePrevious)
                {
                    previousUnjitteredMvp = unjitteredMvp;
                    havePrevious = true;
                }

                Vector2 jitterPixels = options.UsesJitter ? jitter!.Current : Vector2.Zero;
                Matrix4x4 jitteredMvp = options.UsesJitter
                    ? JitterSequence.ApplyJitter(in unjitteredMvp, jitterPixels, renderW, renderH)
                    : unjitteredMvp;

                var uniforms = new CubeScene.FrameUniforms
                {
                    JitteredMvp  = jitteredMvp,
                    CurrentMvp   = unjitteredMvp,
                    PreviousMvp  = previousUnjitteredMvp,
                    Model        = model,
                    RenderExtent = new Vector2(renderW, renderH),
                };
                scene.WriteUniforms(fc.SlotIndex, in uniforms);

                bool captureThisFrame = options.CapturePath is not null && frame + 1 == options.MaxFrames;

                var rec = fc.CommandBuffers.Begin();
                try
                {
                    // D7.1 — attachments in.
                    slot.RecordPreRasterBarriers(ref rec);

                    // D7.2 — two colour attachments (colour, motion vectors)
                    // plus depth, at the RENDER extent.
                    ReadOnlySpan<ColorAttachment> color =
                    [
                        new ColorAttachment
                        {
                            View       = slot.ColorView,
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
                            // Zero motion for background pixels: nothing moved.
                            ClearColor = ClearColor(0f, 0f, 0f, 0f),
                        },
                    ];
                    var renderArea = new VkRect2D { extent = new VkExtent2D { width = renderW, height = renderH } };
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
                            // Far = 1.0: standard depth, which is why
                            // DlssFeatureFlags.DepthInverted stays clear.
                            ClearDepth = 1.0f,
                        },
                    });

                    // Positive-height viewport. A negative-height viewport
                    // would flip Y a second time and silently invert every
                    // derivation in spec D2.
                    rec.SetViewport(new VkViewport
                    {
                        x = 0, y = 0, width = renderW, height = renderH, minDepth = 0, maxDepth = 1,
                    });
                    rec.SetScissor(renderArea);

                    // D7.3 — bind and draw.
                    rec.BindPipeline(in pipeline.Pipeline);

                    Buffer uniformBuffer = scene.Uniforms(fc.SlotIndex);
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
                    rec.PushDescriptorSet(VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS,
                        in pipeline.Layout, set: 0, writes);

                    // A stack-backed span, not `Buffer[] vertexBuffers = [vbo]`:
                    // BindVertexBuffers takes scoped ReadOnlySpan<Buffer>, so
                    // the heap array the other windowed samples use here is not
                    // required by the API (spec E12).
                    ReadOnlySpan<Buffer> vertexBuffers = [scene.VertexBuffer];
                    rec.BindVertexBuffers(0, vertexBuffers);
                    rec.BindIndexBuffer(in scene.IndexBuffer, 0, VkIndexType.VK_INDEX_TYPE_UINT16);
                    rec.DrawIndexed(scene.IndexCount);

                    // D7.4 — the evaluate must sit OUTSIDE any rendering scope.
                    rec.EndRendering();

                    if (options.UsesDlss)
                    {
                        // D7.5 — the layout contract the wrapper cannot hold.
                        slot.RecordPreEvaluateBarriers(ref rec);

                        // D7.6 — the evaluate.
                        s_dlss!.Evaluate(ref rec, new DlssEvaluateInputs
                        {
                            Color         = slot.NgxColor,
                            Depth         = slot.NgxDepth,
                            MotionVectors = slot.NgxMotionVectors,
                            Output        = slot.NgxOutput,
                            JitterOffsetX = jitterPixels.X,
                            JitterOffsetY = jitterPixels.Y,
                            RenderWidth   = renderW,
                            RenderHeight  = renderH,
                            Reset         = resetNextFrame,
                            // MotionVectorScaleX/Y stay at their 1f defaults:
                            // the vectors are already in render pixels and
                            // already point at the previous frame (guide §3.6.3).
                        });
                        resetNextFrame = false;

                        // Nothing draws or dispatches after this, so there is
                        // nothing to rebind here — but EvaluateFeature_C
                        // CLOBBERS the bound pipeline, descriptor sets and
                        // dynamic state (guide §5.2.5). A renderer that draws
                        // UI after DLSS must rebind all of it.

                        slot.RecordPreBlitBarriers(ref rec, fromGeneral: true);
                    }
                    else
                    {
                        // off / bilinear: the same presentation image, written
                        // by a blit instead. Keeping the path uniform is what
                        // stops a reader copying the non-DLSS branch and
                        // losing the Storage | TransferDst pairing.
                        slot.RecordPreUpscaleBlitBarriers(ref rec);

                        ReadOnlySpan<ImageBlitRegion> upscale =
                        [
                            ImageBlitRegion.WholeImage(in slot.Color, in slot.Presentation),
                        ];
                        rec.BlitImage(
                            in slot.Color, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                            in slot.Presentation, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                            upscale,
                            effectiveMode == DlaaMode.Bilinear
                                ? VkFilter.VK_FILTER_LINEAR
                                : VkFilter.VK_FILTER_NEAREST);

                        slot.RecordPreBlitBarriers(ref rec, fromGeneral: false);
                    }

                    // --capture: the presentation image is already in
                    // TRANSFER_SRC_OPTIMAL for the swapchain blit, so the
                    // readback copy shares that barrier rather than adding one.
                    if (captureThisFrame)
                    {
                        captured = true;
                        rec.CopyImageToBuffer(
                            in slot.Presentation, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                            in readback,
                            BufferImageCopy.WholeImage(in slot.Presentation));
                    }

                    // D7.7 — the swapchain image. GetImage, not GetImageHandle:
                    // the blit region below reads the destination's extent.
                    //
                    // The source scope is AllTransfer, NOT TopOfPipe, and it
                    // matches the stage fc.Submit waits on the acquire
                    // semaphore at (below). A layout transition out of
                    // UNDEFINED is a real write to the image, so it must not
                    // begin before the presentation engine is done with it —
                    // and a barrier whose src scope is TopOfPipe is not gated
                    // by a semaphore wait at any later stage. Same shape as
                    // Khronos' swapchain acquire example, with the transfer
                    // stage in place of ColorAttachmentOutput because this
                    // sample's first use of the image is a blit, not a draw.
                    Image swapImage = swap.GetImage(imageIndex);
                    rec.PipelineBarrier(ImageBarrier.Transition(
                        in swapImage,
                        VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                        Stage.AllTransfer, Access.None,
                        Stage.AllTransfer, Access.TransferWrite));

                    // D7.8 — present blit. NEAREST because the extents are
                    // equal, so nearest is exact and says no resampling is
                    // intended.
                    //
                    // This is correct ONLY because Swapchain.GetImage carries
                    // the real extent: Image.FromRaw reports 0x0 on purpose
                    // (#119), and WholeImage over one of those produces a
                    // degenerate destination box that blits nothing.
                    ReadOnlySpan<ImageBlitRegion> present =
                    [
                        ImageBlitRegion.WholeImage(in slot.Presentation, in swapImage),
                    ];
                    rec.BlitImage(
                        in slot.Presentation, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                        in swapImage,         VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                        present, VkFilter.VK_FILTER_NEAREST);

                    // D7.9 — present layout.
                    rec.PipelineBarrier(ImageBarrier.Transition(
                        in swapImage,
                        VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
                        Stage.AllTransfer,   Access.TransferWrite,
                        Stage.BottomOfPipe,  Access.None));

                    // BOTH stage arguments are overridden, and both defaults
                    // would be wrong here — silently, because standard
                    // validation does not check either (synchronization
                    // validation does).
                    //
                    // The defaults assume the swapchain image is a colour
                    // attachment. This sample only ever BLITS into it:
                    //   * waiting on the acquire at ColorAttachmentOutput would
                    //     not gate the blit — transfer stages are not ordered
                    //     after ColorAttachmentOutput — so the blit could run
                    //     while the presentation engine still owns the image;
                    //   * signalling RenderingDone at AllGraphics would not
                    //     wait for the blit either, since
                    //     VK_PIPELINE_STAGE_ALL_GRAPHICS_BIT excludes transfer,
                    //     so present could race the copy.
                    // Any renderer whose last write to the swapchain image is a
                    // copy, blit or compute dispatch has to do this.
                    fc.Submit(queue, ref rec, swap, imageIndex,
                        imageAcquireWaitStage:    Stage.AllTransfer,
                        renderingDoneSignalStage: Stage.AllCommands);
                }
                finally { rec.Dispose(); }

                var pres = swap.Present(queue, imageIndex);
                // Present can only report Success, Suboptimal, OutOfDate or
                // SurfaceLost: Timeout and NotReady are gated `when fromAcquire`
                // in MapPresentationResult, so a present returning either is a
                // broken ICD and throws rather than mapping to a benign retry.
                if (pres is AcquireResult.OutOfDate or AcquireResult.Suboptimal)
                {
                    // No sleep on the false leg: the loop top sees a non-Ready
                    // state on the next iteration and sleeps there.
                    if (TryRecreate(device, swap, ring, in surface, window, preferred))
                        rebuildPending = true;
                }

                previousUnjitteredMvp = unjitteredMvp;
                if (options.UsesJitter) jitter!.Advance();

                frame++;
                if (frame >= options.MaxFrames) break;
            }

            device.WaitIdle();
            Console.WriteLine($"Rendered {frame} frames.");

            if (options.CapturePath is not null && !captured)
            {
                Console.Error.WriteLine(
                    $"No capture written: the run ended before frame {options.MaxFrames}, so the readback " +
                    "copy never executed. Let it run to --frames, or pass a smaller --frames.");
            }
            else if (options.CapturePath is not null && !readback.IsNull)
            {
                // WaitIdle gives availability, not visibility: device writes to
                // a non-coherent host-visible allocation reach the host only
                // after vkInvalidateMappedMemoryRanges. A no-op on coherent
                // memory, which is the desktop case -- and the reason to write
                // it anyway rather than rely on the platform.
                readback.Invalidate();
                PngWriter.Write(options.CapturePath, readback.AsReadOnlySpan<byte>(), (int)outW, (int)outH);
                Console.WriteLine($"Wrote {options.CapturePath} ({outW}x{outH}) from the presentation image.");
            }

            if (options.UsesDlss && s_ngx!.TryGetStats(out DlssStats stats))
                PrintDlssStats(in stats, "after the run");
        }
        finally
        {
            device.WaitIdle();
            for (int i = 0; i < targets.Length; i++) targets[i]?.Dispose();
            readback.Dispose();
            s_dlss?.Dispose();
            s_dlss = null;
            s_ngx?.Dispose();
            s_ngx = null;

            // Unconditional, and therefore in the finally: a run that bailed on
            // a shader-compile failure still reports what the layer saw.
            Console.WriteLine(
                $"Validation: {System.Threading.Volatile.Read(ref s_validationErrors)} error(s), " +
                $"{System.Threading.Volatile.Read(ref s_validationWarnings)} warning(s).");
        }

        // Only the completed path can downgrade to 3; an earlier `return 2` or
        // `return 5` already named a more specific failure.
        return System.Threading.Volatile.Read(ref s_validationErrors) == 0 ? exitCode : 3;
    }

    /// <summary>
    /// <c>VK_ERROR_SURFACE_LOST_KHR</c> reached the frame loop. This is
    /// <b>terminal, not retryable</b>, and the sample stops.
    /// </summary>
    /// <remarks>
    /// <para><c>SurfaceLost</c> maps to <c>SwapchainState.SurfaceLost</c>
    /// without throwing, so unlike a hard error it flows back as an ordinary
    /// <see cref="AcquireResult"/>. Since #222 the state carries the cause, so
    /// this is reached from the single loop-top guard — which both the
    /// acquire and the present path feed — rather than from a
    /// report-and-break at each site. Left unhandled it still falls through,
    /// and the next <c>AcquireNextImage</c> throws out of
    /// <c>ThrowIfNotPresentable</c>, which rejects <c>SurfaceLost</c> exactly
    /// as it rejects <c>Minimized</c>.</para>
    /// <para><b>Do not turn this into a retry.</b> <c>Recreate</c> over the same
    /// <c>VkSurfaceKHR</c> cannot succeed: recovery means destroying and
    /// rebuilding the surface as well — a strict superset of the
    /// <c>OutOfDate</c> path, and the window system's business rather than this
    /// sample's. Routing it through <c>TryRecreate</c> would only fail
    /// differently.</para>
    /// <para>Exit code stays 0 (or 3 if the validation layer complained on the
    /// way): a lost surface is an environment failure, not a defect in the
    /// sample, which is the same posture the no-DLSS skip paths take.</para>
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
    /// when the swapchain is not presentable afterwards: a minimized window,
    /// which is a legal state and not an error (#110), or a surface lost during
    /// the recreate itself, which the loop-top terminal guard turns into a clean
    /// exit on the next iteration (#222).
    /// </summary>
    /// <remarks>
    /// Callers must test this return value rather than <c>swap.Extent</c>:
    /// <c>CreateOrRecreate</c> returns <c>Minimized</c> <i>before</i> assigning
    /// its extent field, so the property keeps its last good non-zero value and
    /// an extent test can never see the minimize.
    /// </remarks>
    private static bool TryRecreate(
        Device device, Swapchain swap, FrameRing ring,
        in Surface surface, SdlWindow window,
        ReadOnlySpan<VkSurfaceFormatKHR> preferredFormats)
    {
        device.WaitIdle();
        SwapchainState state;
        try
        {
            state = swap.Recreate(new SwapchainDescription
            {
                Surface          = surface,
                Width            = window.Width,
                Height           = window.Height,
                PreferredFormats = preferredFormats,
                ImageUsage       = ImageUsage.ColorAttachment | ImageUsage.TransferDst,
            });
        }
        catch (VulkanException e) when (e.Result is VkResult.VK_ERROR_SURFACE_LOST_KHR)
        {
            // The likeliest place a real surface loss is first seen: a
            // display-driver restart shows up as OutOfDate from present, and it
            // is the capability query inside the *next* Recreate that reports
            // the loss. Recreate has already moved swap.State to
            // SwapchainState.SurfaceLost (#222), so returning false hands the
            // stop-or-recreate decision to the loop-top terminal guard instead
            // of unwinding out of Main with a stack trace. It also leaves
            // rebuildPending clear, so the DLSS/target rebuild is not run
            // against a swapchain that is about to be torn down. Deliberately
            // narrow: every other VulkanException -- device loss included --
            // still terminates the sample loudly, because none of them is a
            // state the loop knows how to resume from.
            return false;
        }
        ring.RecycleStaleAcquireSemaphores();
        return state == SwapchainState.Ready;
    }

    // ---------------------------------------------------------------------
    //  Render-extent selection, and everything that depends on it.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Called at start-up and again on every resize. Returns
    /// <see langword="false"/> only when <c>--require-dlss</c> was passed and
    /// the requested mode is not offered at this resolution.
    /// </summary>
    private static bool RebuildForExtent(
        Device device, Queue queue, CommandBufferPool pool, CubeScene scene,
        in DlaaOptions options, VkExtent2D outputExtent, bool memoryBudget,
        FrameTargets?[] targets, ref Buffer readback, ref JitterSequence? jitter,
        ref uint renderW, ref uint renderH, ref uint outW, ref uint outH,
        ref DlaaMode effectiveMode)
    {
        outW = outputExtent.width;
        outH = outputExtent.height;
        effectiveMode = options.Mode;

        // 2. Render extent.
        if (effectiveMode is DlaaMode.Quality or DlaaMode.Bilinear)
        {
            // In bilinear the settings are queried purely to pick a matching
            // extent, so the control renders the same number of pixels as
            // quality does. That is what makes the comparison honest.
            DlssOptimalSettings settings = s_ngx is not null
                ? s_ngx.GetOptimalSettings(outW, outH, DlssQualityMode.MaxQuality)
                : new DlssOptimalSettings
                {
                    // No NGX in bilinear-without-DLSS: MaxQuality is 1/1.5
                    // linear (guide §3.7.1.1's table), which is what NGX
                    // returns for it.
                    IsAvailable  = true,
                    RenderWidth  = (uint)(outW / 1.5f),
                    RenderHeight = (uint)(outH / 1.5f),
                };

            if (!settings.IsAvailable)
            {
                Console.Error.WriteLine(
                    $"DLSS MaxQuality is not offered at {outW}x{outH} on this GPU.");
                if (options.RequireDlss) return false;
                Console.WriteLine("Falling back to --mode dlaa.");
                effectiveMode = DlaaMode.Dlaa;
                renderW = outW; renderH = outH;
            }
            else
            {
                renderW = settings.RenderWidth;
                renderH = settings.RenderHeight;
            }
        }
        else
        {
            renderW = outW;
            renderH = outH;
        }

        // 3. Jitter.
        jitter = new JitterSequence(renderW, renderH, outW, outH);

        // 4. Mip bias — guide §3.5, unmodified, for every DLSS mode including
        // DLAA where it evaluates to -1.0 (spec D6). §3.5.1 warns that
        // high-frequency content can moiré under an aggressive bias; this
        // sample's texture is deliberately high-frequency, and the value is
        // printed below so the observation is reproducible.
        float mipBias = options.UsesDlss ? MathF.Log2(renderW / (float)outW) - 1f : 0f;
        scene.SetMipLodBias(mipBias);

        // 5. Per-slot targets.
        for (int i = 0; i < targets.Length; i++)
        {
            targets[i]?.Dispose();
            targets[i] = FrameTargets.Create(device, renderW, renderH, outW, outH);
        }

        // The capture readback follows the OUTPUT extent, so it is rebuilt here
        // rather than at start-up.
        if (options.CapturePath is not null)
        {
            readback.Dispose();
            readback = device.Allocator.CreateBuffer(
                new BufferDescription { Size = (ulong)outW * outH * 4, Usage = BufferUsage.TransferDst },
                new AllocationDescription
                {
                    Usage = MemoryUsage.AutoPreferHost,
                    Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
                });
        }

        // 6. The feature.
        s_dlss?.Dispose();
        s_dlss = null;
        if (options.UsesDlss && s_ngx is not null)
        {
            if (memoryBudget) PrintHeapBudgets(device.Allocator, "before CreateDlss");

            s_dlssDescription = new DlssFeatureDescription
            {
                RenderWidth  = renderW, RenderHeight = renderH,
                OutputWidth  = outW,    OutputHeight = outH,
                Mode         = effectiveMode == DlaaMode.Quality ? DlssQualityMode.MaxQuality : DlssQualityMode.Dlaa,
                // MotionVectorsLowRes: the vectors are rendered at the render
                //   resolution, the preferred case DLSS dilates internally.
                // AutoExposure: no exposure image is bound.
                // MotionVectorsJittered stays CLEAR: the vectors come from
                //   unjittered matrices (cube.slang).
                // DepthInverted stays CLEAR: CreatePerspectiveFieldOfView gives
                //   near 0 / far 1 (guide §3.8).
                // Hdr stays CLEAR: LDR path, the shader encodes sRGB (D4).
                Flags = DlssFeatureFlags.MotionVectorsLowRes | DlssFeatureFlags.AutoExposure,
            };

            // ImmediateSubmit submits AND WAITS, which is what CreateDlss
            // requires before the first Evaluate: CreateFeature1 records real
            // initialization work.
            queue.ImmediateSubmit(pool, static (ref CommandRecorder recorder) =>
            {
                s_dlss = s_ngx!.CreateDlss(ref recorder, in s_dlssDescription);
            });

            if (memoryBudget) PrintHeapBudgets(device.Allocator, "after CreateDlss");
            if (s_ngx.TryGetStats(out DlssStats stats)) PrintDlssStats(in stats, "after CreateDlss");
        }

        Console.WriteLine(
            $"Mode {ModeName(effectiveMode)}: render {renderW}x{renderH} → output {outW}x{outH}, " +
            $"{jitter.PhaseCount} jitter phases, mip bias {scene.MipLodBias:F2}.");

        return true;
    }

    // ---------------------------------------------------------------------
    //  Helpers.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Builds the model matrix and the UNJITTERED model-view-projection.
    /// <c>proj.M22 *= -1f</c> gives a y-DOWN NDC and a [0,1] depth range, which
    /// is the coordinate system every derivation in spec D2 assumes.
    /// </summary>
    private static void BuildMatrices(float seconds, float aspect, out Matrix4x4 model, out Matrix4x4 mvp)
    {
        model =
            Matrix4x4.CreateRotationY(seconds * 0.35f) *
            Matrix4x4.CreateRotationX(seconds * 0.22f);

        // 40 degrees at distance 7 rather than a wider lens up close. The cube
        // is 2 units on a side, so 3.46 corner to corner; at 60 degrees and
        // distance 4.5 its near and far corners sit at w = 2.80 and w = 6.20,
        // a 2.22x depth ratio ACROSS ONE OBJECT, and the foreshortening reads
        // as if the cube were sheared. This pair puts them at 5.30 and 8.70
        // (1.64x) while keeping the cube the same size on screen, so nothing
        // downstream — framing, jitter phase count, render extents — moves.
        var view = Matrix4x4.CreateLookAt(
            cameraPosition: new Vector3(0, 0, -7.0f),
            cameraTarget:   Vector3.Zero,
            cameraUpVector: Vector3.UnitY);

        var proj = Matrix4x4.CreatePerspectiveFieldOfView(
            fieldOfView:       40f * (MathF.PI / 180f),
            aspectRatio:       aspect,
            nearPlaneDistance: 0.1f,
            farPlaneDistance:  100f);
        proj.M22 *= -1f;

        mvp = model * view * proj;
    }

    /// <summary>
    /// A developer-machine convenience for <b>this repository</b>: walk up from
    /// the binary until a directory contains <c>native/ngx</c>, then hand NGX
    /// the staged <c>rel/</c> folder.
    /// </summary>
    /// <remarks>
    /// This is <b>not</b> the deployment model. A shipped application puts the
    /// feature DLL beside its executable (<c>native/ngx/README.md:31-34</c>),
    /// which is what the csproj's two <c>None</c> items do.
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

    private static Utf8Name[] ConcatExtensions(Utf8Name[] first, ReadOnlySpan<Utf8Name> second)
    {
        var all = new Utf8Name[first.Length + second.Length];
        first.AsSpan().CopyTo(all);
        second.CopyTo(all.AsSpan(first.Length));
        return all;
    }

    private static Utf8Name[] BuildDeviceExtensions(ReadOnlySpan<Utf8Name> ngxNames, bool memoryBudget)
    {
        var all = new Utf8Name[1 + ngxNames.Length + (memoryBudget ? 1 : 0)];
        all[0] = VulkanExtensions.KhrSwapchain;
        ngxNames.CopyTo(all.AsSpan(1));
        if (memoryBudget) all[^1] = VulkanExtensions.ExtMemoryBudget;
        return all;
    }

    /// <summary>
    /// VMA's <c>AllocationBytes</c> counts only what VMA allocated;
    /// <c>Usage</c> is the driver's own figure for the heap. DLSS's history and
    /// scratch surfaces are allocated inside the driver, so they move
    /// <c>Usage</c> and never <c>AllocationBytes</c> — which is the whole point
    /// of pairing <c>EnableMemoryBudget</c> with <c>VK_EXT_memory_budget</c>.
    /// </summary>
    private static void PrintHeapBudgets(Allocator allocator, string label)
    {
        Span<MemoryHeapBudget> budgets = stackalloc MemoryHeapBudget[16];
        int count = allocator.GetHeapBudgets(budgets);
        for (int i = 0; i < count; i++)
        {
            MemoryHeapBudget b = budgets[i];
            if (b.Budget == 0) continue;
            Console.WriteLine(
                $"  VRAM [{label}] heap {b.HeapIndex}: VMA allocated {Mib(b.AllocationBytes)} MiB, " +
                $"driver usage {Mib(b.Usage)} MiB of {Mib(b.Budget)} MiB budget.");
        }

        static ulong Mib(ulong bytes) => bytes / (1024 * 1024);
    }

    private static void PrintDlssStats(in DlssStats stats, string label)
    {
        Console.WriteLine(
            $"  DLSS [{label}]: {stats.VramAllocatedBytes / (1024 * 1024)} MiB of driver-side VRAM " +
            $"(invisible to VMA), OptLevel {stats.OptLevel}, dev branch {stats.IsDevSnippetBranch}.");

        // A rel/ feature DLL reports OptLevel 40 and no dev branch. Anything
        // else means a dev/ build got deployed — that one carries an on-screen
        // watermark (#218 OPEN-3).
        if (stats.OptLevel != 40 || stats.IsDevSnippetBranch)
        {
            Console.Error.WriteLine(
                "WARNING: this looks like a dev/ build of nvngx_dlss. Expected OptLevel 40 and no dev " +
                "snippet branch; the dev build watermarks the screen and must never be redistributed.");
        }
    }

    /// <summary>
    /// DLSS turned out to be unavailable. Returns <see langword="true"/> when
    /// the run can carry on regardless — which is only the <c>bilinear</c>
    /// control, whose use of NGX is limited to asking for a render extent.
    /// Otherwise <paramref name="exitCode"/> carries the process exit code: 5
    /// under <c>--require-dlss</c>, 0 for a clean skip.
    /// </summary>
    private static bool TryContinueWithoutNgx(in DlaaOptions options, string? reason, out int exitCode)
    {
        // reason is null when the caller already printed the wrapper's own
        // diagnosis, which is more specific than anything restated here.
        if (reason is not null) Console.Error.WriteLine(reason);

        if (options.RequireDlss)
        {
            Console.Error.WriteLine("--require-dlss was passed, so this is a failure rather than a skip.");
            exitCode = 5;
            return false;
        }

        if (options.UsesDlss)
        {
            Console.WriteLine("Skipping cleanly. Pass --mode off or --mode bilinear to run without DLSS.");
            exitCode = 0;
            return false;
        }

        Console.WriteLine("Continuing without NGX: the bilinear control derives its render extent from the " +
            "guide's MaxQuality ratio (§3.7.1.1) instead of asking NGX for it.");
        exitCode = 0;
        return true;
    }

    private static string ModeName(DlaaMode mode) => mode switch
    {
        DlaaMode.Dlaa     => "dlaa",
        DlaaMode.Quality  => "quality",
        DlaaMode.Off      => "off",
        _                 => "bilinear",
    };

    private static bool IsSrgbFormat(VkFormat format) => format is
        VkFormat.VK_FORMAT_R8G8B8A8_SRGB or
        VkFormat.VK_FORMAT_B8G8R8A8_SRGB or
        VkFormat.VK_FORMAT_A8B8G8R8_SRGB_PACK32 or
        VkFormat.VK_FORMAT_R8G8B8_SRGB or
        VkFormat.VK_FORMAT_B8G8R8_SRGB;

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
