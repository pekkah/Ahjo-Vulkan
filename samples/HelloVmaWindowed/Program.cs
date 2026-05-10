using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Ahjo.Vulkan;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Samples.HelloVmaWindowed;

/// <summary>
/// Windowed companion to <c>HelloVma</c>. The headless variant tours
/// the static-allocation patterns (device-local + staging, host-mapped
/// readback, one-shot uploads); this one fills in the
/// <i>frame-loop-only</i> story:
/// <list type="bullet">
///   <item><b>Per-frame in-flight resource ring</b> — why every buffer
///   the CPU writes each frame needs <c>FramesInFlight</c> copies, not
///   one, and what goes wrong if you cheat.</item>
///   <item><b>Persistent map + sequential write under animation</b> —
///   the per-frame UBO is rewritten every frame through a cached
///   <see cref="System.Span{T}"/> off the live mapped pointer; no
///   <c>vmaMapMemory</c> in the hot loop.</item>
///   <item><b>The per-slot <see cref="StagingUploader"/> that
///   <see cref="FrameRing"/> already owns</b> — accessible as
///   <see cref="FrameContext.Staging"/>; the ring auto-resets it on
///   slot rotation, so you just <c>Upload</c> + record the consuming
///   copy and move on.</item>
/// </list>
/// Press <kbd>Esc</kbd> to quit. Cross-platform via the SDL3 shim.
/// <c>--frames N</c> closes the window after N successfully-presented
/// frames so the sample doubles as a smoke test in CI.
/// </summary>
internal static unsafe class Program
{
    /// <summary>
    /// Per-frame UBO payload. <c>std140</c> compatible: <c>mat4</c> is
    /// 16-byte aligned at offset 0, <c>vec4</c> is 16-byte aligned at
    /// offset 64. Total 80 bytes — well under the spec's
    /// <c>maxUniformBufferRange</c> floor of 16 KiB.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Frame
    {
        public Matrix4x4 Transform;
        public Vector4   Tint;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex
    {
        public Vector2 Position;
        public Vector3 Color;
        public Vertex(Vector2 position, Vector3 color)
        { Position = position; Color = color; }
    }

    private const uint FramesInFlight = 2;

    // Validation messages fire from arbitrary driver threads — keep the
    // counters static so the callback closure is captureless.
    private static int s_validationErrors;
    private static int s_validationWarnings;

    private static int Main(string[] args)
    {
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
        string vertSpv    = Path.Combine(shadersDir, "vma.vert.spv");
        string fragSpv    = Path.Combine(shadersDir, "vma.frag.spv");
        if (!File.Exists(vertSpv) || !File.Exists(fragSpv))
        {
            Console.Error.WriteLine($"Missing compiled shaders. Expected:\n  {vertSpv}\n  {fragSpv}");
            return 2;
        }

        using var window = new SdlWindow("Ahjo.Vulkan — HelloVmaWindowed (Esc to quit)", 1024, 768,
            hidden: false, resizable: true);

        Utf8Name[] instanceExts = SdlWindow.GetRequiredVulkanInstanceExtensions();
        // EnableValidation loads VK_LAYER_KHRONOS_validation + the
        // VK_EXT_debug_utils extension; DebugCallback receives every
        // message the layer emits. Required SDK install: install the
        // Vulkan SDK (or the standalone validation layers) and ensure
        // they are discoverable through the loader's search path.
        using var instance = Instance.Create(new InstanceDescription
        {
            Extensions       = instanceExts,
            EnableValidation = true,
            DebugCallback    = OnValidationMessage,
        });

        using var surface = window.CreateVulkanSurface(instance);
        using var device  = CreatePresentDevice(instance, in surface, out uint family);
        Allocator allocator = device.Allocator;

        var swapDesc = new SwapchainDescription
        {
            Surface = surface,
            Width   = window.Width,
            Height  = window.Height,
        };
        // ---------------------------------------------------------------
        //  KNOWN VALIDATION FINDING (wrapper-level, not VMA).
        // ---------------------------------------------------------------
        //  Running this sample with the validation layer enabled trips
        //  VUID-vkQueueSubmit2-semaphore-03868 a few times: the per-slot
        //  `RenderingDone` semaphore that FrameRing owns can be
        //  re-signaled by the next frame's submit before the prior
        //  vkQueuePresent has had its acquire consumed. The textbook
        //  fix is per-acquired-image semaphores indexed by `imageIndex`
        //  (the validator quotes it verbatim) — a FrameRing-level
        //  change, not anything this sample can fix from the outside.
        //  Aligning ImageCount with FramesInFlight via PreferredImageCount
        //  doesn't help because acquire order isn't strictly round-robin.
        //  The errors are unrelated to VMA — every other path
        //  (allocator, per-frame UBO writes, persistent map, flush)
        //  is validation-clean.
        // ---------------------------------------------------------------
        using var swap  = new Swapchain(device, in swapDesc);
        var queue       = device.GetQueue(family, 0);

        // ---------------------------------------------------------------
        //  STARTUP — Static device-local vertex buffer (asset-load case).
        // ---------------------------------------------------------------
        // Same pattern HelloVma's STEP 1 + STEP 5 walk through, condensed
        // into one block: device-local memory + StagingBatch upload. The
        // vertices never change, so one upload at startup covers every
        // frame for the lifetime of the process.
        // ---------------------------------------------------------------
        Vertex[] cpuVertices =
        [
            new(new(-0.6f,  0.5f), new(1.0f, 0.0f, 0.0f)),
            new(new( 0.6f,  0.5f), new(0.0f, 1.0f, 0.0f)),
            new(new( 0.0f, -0.6f), new(0.0f, 0.0f, 1.0f)),
        ];
        ulong vbBytes = (ulong)(cpuVertices.Length * sizeof(Vertex));

        using var vertexBuffer = allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = vbBytes,
                Usage = BufferUsage.VertexBuffer | BufferUsage.TransferDst,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using (var startupPool  = new CommandBufferPool(device, family))
        using (var startupBatch = new StagingBatch(allocator))
        {
            startupBatch.EnqueueUpload<Vertex>(cpuVertices, in vertexBuffer);
            startupBatch.Flush(queue, startupPool);
        }

        // ---------------------------------------------------------------
        //  STARTUP — Per-frame UBO ring.
        // ---------------------------------------------------------------
        //  This is the headed-only chapter of the VMA story. Every
        //  buffer the CPU writes during a frame and the GPU reads
        //  during the same frame must exist in <c>FramesInFlight</c>
        //  copies — one per slot in the ring. Why?
        //
        //  When the swapchain is double-buffered and FramesInFlight = 2:
        //    * Frame N: CPU writes UBO(N), records the draw, submits.
        //    * Frame N+1: CPU starts on UBO(N+1) *while the GPU is
        //      still consuming UBO(N)*.
        //
        //  If we shared a single UBO across slots, frame N+1's CPU write
        //  would race the GPU's read of frame N. The Vulkan validation
        //  layer would (rightly) yell about a write-after-read hazard,
        //  and on real hardware you'd get tearing, glitches, or worse.
        //
        //  The fix is a small ring of UBOs sized to FramesInFlight. Each
        //  slot writes "its own" UBO; the FrameRing's per-slot fence
        //  guarantees the GPU is done with slot K before slot K rotates
        //  back in. So UBO(K) is provably idle when CPU writes it.
        //
        //  Each UBO is host-visible + persistent-mapped + sequential
        //  write — the same pattern as HelloVma's STEP 2, but per-slot.
        //  Persistent mapping pays off here: zero map/unmap overhead in
        //  the hot loop, just an array index + Span<T> write per frame.
        // ---------------------------------------------------------------
        Buffer[] frameUbos = new Buffer[FramesInFlight];
        for (uint i = 0; i < FramesInFlight; i++)
        {
            frameUbos[i] = allocator.CreateBuffer(
                new BufferDescription
                {
                    Size  = (ulong)sizeof(Frame),
                    Usage = BufferUsage.UniformBuffer,
                },
                new AllocationDescription
                {
                    Usage = MemoryUsage.AutoPreferHost,
                    Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
                });
        }

        // ---------------------------------------------------------------
        //  Pipeline plumbing — pure Vulkan, identical shape to HelloVma.
        // ---------------------------------------------------------------
        DescriptorBinding[] bindings =
        [
            new()
            {
                Slot   = 0,
                Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                Count  = 1,
                // Both stages read from the UBO — vertex pulls the
                // transform, fragment pulls the tint.
                Stages = ShaderStages.Vertex | ShaderStages.Fragment,
            },
        ];
        using var setLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings       = bindings,
            PushDescriptor = true,
        });

        DescriptorSetLayout[] setLayouts = [setLayout];
        using var pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts = setLayouts,
        });

        using var vertBlob = SpirvBlob.Load(vertSpv);
        using var fragBlob = SpirvBlob.Load(fragSpv);
        using var vMod = device.CreateShaderModule(vertBlob.Words);
        using var fMod = device.CreateShaderModule(fragBlob.Words);

        VertexBindingDescription[] vBindings =
        [
            new() { Slot = 0, Stride = (uint)sizeof(Vertex), InputRate = VkVertexInputRate.VK_VERTEX_INPUT_RATE_VERTEX },
        ];
        VertexAttributeDescription[] vAttrs =
        [
            new() { Location = 0, Binding = 0, Format = VkFormat.VK_FORMAT_R32G32_SFLOAT,    Offset = 0 },
            new() { Location = 1, Binding = 0, Format = VkFormat.VK_FORMAT_R32G32B32_SFLOAT, Offset = (uint)sizeof(Vector2) },
        ];

        VkFormat swapFormat = swap.Format;
        ReadOnlySpan<VkFormat> colorFormats = stackalloc VkFormat[] { swapFormat };
        using var pipeline = device.BuildGraphicsPipeline()
            .WithStages(in vMod, in fMod)
            .WithVertexInput(new VertexInputDescription { Bindings = vBindings, Attributes = vAttrs })
            .WithDynamicRendering(colorFormats)
            .WithLayout(in pipelineLayout)
            .Build();

        // ---------------------------------------------------------------
        //  FrameRing wires up everything the per-frame loop needs:
        //   * A per-slot CommandBufferPool (auto-reset on rotation).
        //   * A per-slot in-flight Fence (this is the throttle that
        //     makes per-slot UBOs safe — see the ring comment above).
        //   * A per-slot StagingUploader (auto Reset() on rotation),
        //     reachable through fc.Staging — exactly the per-frame ring
        //     case mentioned in HelloVma's STEP 6 commentary.
        // ---------------------------------------------------------------
        using var ring = new FrameRing(device, framesInFlight: FramesInFlight, queueFamily: family);

        Console.WriteLine($"Swapchain: {swap.Format} {swap.Extent.width}x{swap.Extent.height}, {swap.ImageCount} images, {swap.PresentMode}");
        Console.WriteLine($"Per-frame UBO ring: {FramesInFlight} × {sizeof(Frame)} bytes (persistent-mapped, sequential write).");
        Console.WriteLine($"UBO host-coherent: {frameUbos[0].IsHostCoherent}  (Flush is " +
            (frameUbos[0].IsHostCoherent ? "no-op)" : "real call)"));

        var clock = Stopwatch.StartNew();
        ulong frame = 0;
        try
        {
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

                // -------------------------------------------------------
                //  Per-frame UBO write.
                // -------------------------------------------------------
                //  fc.SlotIndex picks "this frame's" UBO out of the ring.
                //  Slot K's UBO is provably idle: the FrameRing waited
                //  on slot K's in-flight fence inside BeginFrame() before
                //  rotating it back in. So we can write through the
                //  persistent-mapped pointer with no extra synchronisation.
                //
                //  AsSpan<Frame>() returns a Span<Frame> over the live
                //  GPU memory — no map/unmap, no copy, no allocation.
                //  Flush() afterwards is a no-op on coherent memory and
                //  the right call on non-coherent.
                // -------------------------------------------------------
                Buffer ubo = frameUbos[fc.SlotIndex];
                float t = (float)clock.Elapsed.TotalSeconds;
                float aspect = swap.Extent.height == 0 ? 1f : (float)swap.Extent.width / swap.Extent.height;
                ubo.AsSpan<Frame>()[0] = BuildFrame(t, aspect);
                ubo.Flush();

                var rec = fc.CommandBuffers.Begin();
                try
                {
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

                    DescriptorWrite[] writes =
                    [
                        DescriptorWrite.Buffer(
                            binding: 0, arrayElement: 0,
                            VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                            BufferDescriptorWrite.Of(in ubo)),
                    ];
                    rec.PushDescriptorSet(VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS,
                        in pipelineLayout, set: 0, writes);

                    Buffer[] vertexBuffers = [vertexBuffer];
                    rec.BindVertexBuffers(0, vertexBuffers);
                    rec.Draw(vertexCount: (uint)cpuVertices.Length);

                    rec.EndRendering();

                    RecordSwapchainBarrier(ref rec, swap, imageIndex,
                        from: VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                        to:   VkImageLayout.VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
                        srcStage: Stage.ColorAttachmentOutput, srcAccess: Access.ColorAttachmentWrite,
                        dstStage: Stage.BottomOfPipe,          dstAccess: Access.None);

                    // Swapchain-aware submit: waits on fc.ImageAcquired at
                    // ColorAttachmentOutput (matches the
                    // UNDEFINED→COLOR_ATTACHMENT_OPTIMAL barrier above),
                    // signals fc.RenderingDone at AllGraphics so the
                    // following swap.Present's wait sees a real signal.
                    // Passing the stage args explicitly is what selects
                    // this overload over the headless `Submit(queue, ref rec)`
                    // — the latter doesn't wire either semaphore, which
                    // validation flags as a present-without-signal hazard.
                    fc.Submit(queue, ref rec,
                        imageAcquireWaitStage:    Stage.ColorAttachmentOutput,
                        renderingDoneSignalStage: Stage.AllGraphics);
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

            int errors   = System.Threading.Volatile.Read(ref s_validationErrors);
            int warnings = System.Threading.Volatile.Read(ref s_validationWarnings);
            Console.WriteLine($"Validation: {errors} error(s), {warnings} warning(s).");
            return errors == 0 ? 0 : 4;
        }
        finally
        {
            // VMA-backed buffers must be disposed before the Allocator
            // (owned by Device, disposed at the end of `using var device`).
            // The allocator's Dispose path checks for outstanding
            // allocations and writes a warning to stderr if any survive.
            for (int i = 0; i < frameUbos.Length; i++) frameUbos[i].Dispose();
        }
    }

    /// <summary>
    /// Routes every validation/debug message to stderr and tallies
    /// errors and warnings into the static counters. The wrapper marshals
    /// the native callback into a managed <see cref="DebugMessage"/> so
    /// we don't have to touch any unmanaged-string plumbing.
    /// </summary>
    private static void OnValidationMessage(DebugMessage msg)
    {
        bool isError = (msg.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0;
        bool isWarn  = (msg.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT) != 0;
        if (isError) System.Threading.Interlocked.Increment(ref s_validationErrors);
        if (isWarn)  System.Threading.Interlocked.Increment(ref s_validationWarnings);
        // Suppress INFO/VERBOSE — the loader emits a lot of those at
        // startup and they drown out actual validation findings.
        if (!isError && !isWarn) return;
        string tag = isError ? "ERROR" : "WARN ";
        Console.Error.WriteLine($"[VK {tag}] {msg.MessageIdName ?? "?"}: {msg.Message}");
    }

    /// <summary>
    /// Builds the per-frame UBO payload: a 2D rotation around Z, a small
    /// scale wobble, and aspect correction so the triangle stays square
    /// across resizes; combined with a slowly-pulsing tint.
    /// </summary>
    private static Frame BuildFrame(float seconds, float aspect)
    {
        float scale = 0.9f + 0.1f * MathF.Sin(seconds * 1.7f);
        Matrix4x4 transform =
            Matrix4x4.CreateScale(scale, scale, 1f) *
            Matrix4x4.CreateRotationZ(seconds * 0.6f) *
            // Aspect correction: shrink X on wide windows so the triangle
            // doesn't horizontally stretch.
            Matrix4x4.CreateScale(aspect >= 1f ? 1f / aspect : 1f,
                                  aspect >= 1f ? 1f          : aspect, 1f);

        float pulse = 0.5f + 0.5f * MathF.Sin(seconds * 2.3f);
        var tint = new Vector4(
            0.7f + 0.3f * pulse,
            0.7f + 0.3f * (1f - pulse),
            0.9f,
            1.0f);

        return new Frame { Transform = transform, Tint = tint };
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
