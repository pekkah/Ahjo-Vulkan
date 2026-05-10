using System.Numerics;
using System.Runtime.InteropServices;
using Ahjo.Vulkan;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Utilities;

namespace Ahjo.Vulkan.Samples.HelloVma;

/// <summary>
/// Headless guide-style walkthrough of the wrapper's VMA surface
/// (<see cref="Allocator"/>, <see cref="Buffer"/>, <see cref="Image"/>,
/// <see cref="StagingBatch"/>, <see cref="StagingUploader"/>, plus the
/// host-mapped/flush/invalidate plumbing). Renders a small RGB triangle
/// to a 512×512 offscreen image and writes <c>hellovma.png</c> next to
/// the executable. Every memory allocation is annotated with the why
/// — <i>which</i> <see cref="MemoryUsage"/> and which
/// <see cref="AllocationFlags"/>, and what would change if you picked a
/// different combination.
/// </summary>
/// <remarks>
/// <para><b>What this sample teaches.</b> Four canonical patterns:</para>
/// <list type="bullet">
///   <item><b>Device-local + staged upload</b> (vertex/index buffers,
///   most images, anything the GPU touches frequently and the CPU
///   touches once).</item>
///   <item><b>Host-visible + persistent map + sequential write</b>
///   (per-frame uniform buffers, dynamic vertex buffers, anything the
///   CPU writes every frame and the GPU reads once).</item>
///   <item><b>Host-visible + random access + invalidate</b> (readback
///   buffers, screenshots, GPU-side compute results).</item>
///   <item><b>Two staging helpers</b>: <see cref="StagingBatch"/> for
///   the asset-load case (enqueue many, flush once, block) and
///   <see cref="StagingUploader"/> for the per-frame ring case
///   (bump-allocate, copy, no extra syncs).</item>
/// </list>
/// <para>The render itself is intentionally minimal — three vertices,
/// one draw, no depth, no swapchain — so the comments can stay focused
/// on memory.</para>
/// </remarks>
internal static unsafe class Program
{
    private const uint Width  = 512;
    private const uint Height = 512;

    // Validation messages can fire from any thread the driver dispatches
    // them on — make the counters static so the callback closure
    // doesn't have to capture a ref local.
    private static int s_validationErrors;
    private static int s_validationWarnings;

    /// <summary>
    /// Vertex layout matching the GLSL <c>vma.vert</c> input declaration:
    /// <c>vec2 inPos</c> at location 0, <c>vec3 inColor</c> at location 1.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex
    {
        public Vector2 Position;
        public Vector3 Color;
        public Vertex(Vector2 position, Vector3 color)
        { Position = position; Color = color; }
    }

    /// <summary>
    /// Fragment-side uniform buffer payload — std140-compatible because
    /// it's a single <c>vec4</c> (16 bytes, naturally aligned).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Tint
    {
        public Vector4 Color;
    }

    private static int Main(string[] args)
    {
        string outPath = args.Length > 0 ? args[0]
            : Path.Combine(AppContext.BaseDirectory, "hellovma.png");
        string shadersDir = Path.Combine(AppContext.BaseDirectory, "Shaders");
        string vertSpv    = Path.Combine(shadersDir, "vma.vert.spv");
        string fragSpv    = Path.Combine(shadersDir, "vma.frag.spv");
        if (!File.Exists(vertSpv) || !File.Exists(fragSpv))
        {
            Console.Error.WriteLine($"Missing compiled shaders. Expected:\n  {vertSpv}\n  {fragSpv}");
            return 2;
        }

        // ---------------------------------------------------------------
        //  STEP 0 — Instance, device, and *the* allocator.
        // ---------------------------------------------------------------
        // The wrapper has no raw vkCreateBuffer / vkCreateImage path.
        // Every Buffer / Image goes through Allocator, and Device owns
        // exactly one Allocator that lives for the device's lifetime —
        // accessed via `device.Allocator`. You don't have to construct
        // VmaAllocator yourself or feed it function pointers; Device.Create
        // does that on your behalf. (For the curious: see
        // src/Ahjo.Vulkan/Memory/Allocator.cs — it loads the Vulkan
        // loader, threads vkGetInstanceProcAddr / vkGetDeviceProcAddr
        // into VMA, and pins vulkanApiVersion at 1.2 to dodge a
        // long-standing lavapipe / Mesa issue with optional 1.3 imports.)
        // ---------------------------------------------------------------
        // EnableValidation = true loads VK_LAYER_KHRONOS_validation +
        // VK_EXT_debug_utils, and DebugCallback receives every message
        // the layer emits. Required SDK install: install the Vulkan SDK
        // (or the standalone validation layers) and ensure the layer is
        // discoverable through the loader's search path. The wrapper
        // throws a helpful error from Instance.Create if the layer is
        // requested but not installed.
        using var instance = Instance.Create(new InstanceDescription
        {
            EnableValidation = true,
            DebugCallback    = OnValidationMessage,
        });
        using var device   = CreateGraphicsDevice(instance, out uint family);
        Allocator allocator = device.Allocator;

        var queue = device.GetQueue(family, 0);
        using var cmdPool = new CommandBufferPool(device, family);

        // ---------------------------------------------------------------
        //  STEP 1 — Vertex buffer: device-local + staged upload.
        // ---------------------------------------------------------------
        // Why device-local? The GPU reads these vertices on every draw
        // and the CPU never reads them — this is the textbook case for
        // `MemoryUsage.AutoPreferDevice`. VMA picks DEVICE_LOCAL memory,
        // which on a discrete GPU is VRAM (fastest GPU-side access,
        // not directly visible to the CPU at all).
        //
        // Because device-local memory isn't host-mappable on most
        // discrete GPUs, we can't write the vertices directly. The
        // canonical pattern is:
        //   1. Allocate a host-visible *staging* buffer.
        //   2. memcpy the data into the staging buffer (CPU side).
        //   3. Record a vkCmdCopyBuffer from staging → device-local.
        //   4. Submit + wait — staging buffer can now be freed.
        //
        // The wrapper bundles all of that into `StagingBatch` (see
        // STEP 4 below). For the buffer creation itself, all you say is
        // "this lives on the GPU" + "it'll receive a TransferDst copy".
        // ---------------------------------------------------------------
        Vertex[] cpuVertices =
        [
            new(new(-0.6f,  0.5f), new(1.0f, 0.0f, 0.0f)), // bottom-left,  red
            new(new( 0.6f,  0.5f), new(0.0f, 1.0f, 0.0f)), // bottom-right, green
            new(new( 0.0f, -0.6f), new(0.0f, 0.0f, 1.0f)), // top-center,   blue
        ];
        ulong vbBytes = (ulong)(cpuVertices.Length * sizeof(Vertex));

        using var vertexBuffer = allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = vbBytes,
                // TransferDst = "the GPU will receive copies into this".
                // VertexBuffer = "bind it as vertex input to a draw".
                Usage = BufferUsage.VertexBuffer | BufferUsage.TransferDst,
            },
            new AllocationDescription
            {
                // Hint: prefer fast GPU-side access. On a discrete GPU
                // VMA picks DEVICE_LOCAL, which is not host-mappable.
                // On an integrated GPU (UMA) DEVICE_LOCAL is *also*
                // HOST_VISIBLE; VMA uses the same hint either way and
                // picks the right physical heap.
                Usage = MemoryUsage.AutoPreferDevice,
                // No Mapped / HostAccess* flags — we won't touch this
                // buffer from the CPU. Trying to AsSpan/Map it would
                // throw at runtime.
                Flags = AllocationFlags.None,
            });

        // ---------------------------------------------------------------
        //  STEP 2 — Uniform buffer: host-visible + persistent map +
        //           sequential write.
        // ---------------------------------------------------------------
        // Why host-visible? We update the tint every "frame" (here just
        // once, but the shape is the same for a real render loop) and
        // the GPU reads it once per draw. Going through staging would
        // be two copies + a barrier per frame for a 16-byte payload —
        // pure overhead. Letting the CPU write straight into a
        // host-visible UBO is faster *and* simpler.
        //
        // `MemoryUsage.AutoPreferHost` says "land this on a host-visible
        // heap." Combined with the flag pair below it gives us VMA's
        // preferred per-frame UBO shape:
        //
        //   * `HostAccessSequentialWrite` — promises VMA you'll only
        //     write this from the CPU, never read it back. VMA prefers
        //     write-combined (uncached) memory in that case, which is
        //     fast for streaming writes but very slow for reads. *Never
        //     read* a sequential-write buffer from the CPU; it'll hit
        //     uncached memory and your perf numbers will look haunted.
        //
        //   * `Mapped` — keeps the allocation persistently mapped, so
        //     `Buffer.AsSpan<T>()` returns a Span<T> over the GPU memory
        //     directly with zero per-frame map/unmap calls. Idiomatic
        //     for any buffer the CPU touches every frame.
        //
        // Coherency: most desktop drivers' host-visible memory is also
        // HOST_COHERENT, so writes are visible to the GPU automatically
        // after the next vkQueueSubmit's implicit memory-domain flush.
        // Mobile and a few BAR-only setups expose non-coherent host
        // memory — in that case you must call `Buffer.Flush()` after
        // your writes. The wrapper's `Flush()` is a no-op when the
        // allocation is coherent, so calling it unconditionally is the
        // safe portable idiom and we do exactly that below.
        // ---------------------------------------------------------------
        using var tintUbo = allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = (ulong)sizeof(Tint),
                // UNIFORM_BUFFER lets it bind as `uniform Tint { ... }`
                // on the descriptor side. No TransferDst — the CPU
                // writes straight into it.
                Usage = BufferUsage.UniformBuffer,
            },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });

        // Persistent-mapped + sequential write means we just grab a span
        // over the live GPU memory and copy in.
        Span<Tint> tintSpan = tintUbo.AsSpan<Tint>();
        tintSpan[0] = new Tint { Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };
        // Always-safe coherent-aware flush. Skipped at runtime when the
        // allocation already carries HOST_COHERENT_BIT — see
        // `Buffer.IsHostCoherent` if you want to branch yourself.
        tintUbo.Flush();
        Console.WriteLine($"UBO is host-coherent: {tintUbo.IsHostCoherent}  (flush is " +
            (tintUbo.IsHostCoherent ? "no-op)" : "real call)"));

        // ---------------------------------------------------------------
        //  STEP 3 — Render-target image: device-local, no staging.
        // ---------------------------------------------------------------
        // A color attachment never needs initial pixel data — it's
        // written by the rasterizer. So we ask VMA for device-local
        // memory and skip staging entirely. Same hint as the vertex
        // buffer; same reasoning: the GPU is the only consumer.
        //
        // Note `TransferSrc` in the usage: we'll later vkCmdCopyImageToBuffer
        // out of this image into a host-visible readback (STEP 4), and
        // every Vulkan resource has to declare every transfer role it'll
        // play at create time. This is a Vulkan rule, not a VMA one.
        // ---------------------------------------------------------------
        using var renderTarget = allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = Width, Height = Height, Depth = 1,
                MipLevels     = 1, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                // OPTIMAL tiling is opaque GPU layout (swizzled, mipped,
                // best for sampling/rasterizing). Switching to LINEAR
                // would let the CPU read the image directly but kills
                // GPU performance and is rarely the right call —
                // readback through a host-visible *buffer* is faster
                // and easier to reason about (and is what we do below).
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.ColorAttachment | ImageUsage.TransferSrc,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using var renderTargetView = renderTarget.CreateView(device, new ImageViewDescription
        {
            ViewType     = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect       = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            BaseMipLevel = 0, LevelCount = 1, BaseArrayLayer = 0, LayerCount = 1,
        });

        // ---------------------------------------------------------------
        //  STEP 4 — Readback buffer: host-visible + random access +
        //           invalidate-before-read.
        // ---------------------------------------------------------------
        // Reverse direction from STEP 2: GPU writes, CPU reads. The flag
        // shape is therefore different.
        //
        //   * `HostAccessRandom` (not SequentialWrite) — tells VMA the
        //     CPU may read and seek anywhere in this buffer. VMA picks
        //     write-back / cached host-visible memory, which is much
        //     faster for the CPU to read than the write-combined memory
        //     SequentialWrite would request.
        //
        //   * `Mapped` — same persistent map idiom; we'll get a
        //     ReadOnlySpan<byte> over the readback bytes via
        //     `Buffer.AsReadOnlySpan<byte>()`.
        //
        // Direction-specific cache management:
        //   * Pre-write (CPU → GPU)  → `Flush`      after writing.
        //   * Pre-read  (GPU → CPU)  → `Invalidate` before reading.
        // The wrapper's `Invalidate` is a no-op on coherent memory just
        // like `Flush`; calling it unconditionally is correct.
        // ---------------------------------------------------------------
        const uint Bpp = 4; // R8G8B8A8 = 4 bytes per pixel.
        ulong readbackBytes = Width * Height * Bpp;
        using var readback = allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = readbackBytes,
                // TransferDst because the GPU writes into us via
                // vkCmdCopyImageToBuffer. No vertex/uniform/etc role.
                Usage = BufferUsage.TransferDst,
            },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        // ---------------------------------------------------------------
        //  STEP 5 — StagingBatch: bulk one-shot uploads at asset load.
        // ---------------------------------------------------------------
        // For the device-local vertex buffer we still need to get the
        // CPU bytes onto the GPU. `StagingBatch` is the right shape for
        // the asset-load path: enqueue N uploads, flush once, block
        // until they're all on the GPU. Internally it bump-allocates
        // host-visible chunks (4 MiB default), records a single
        // command buffer with one vkCmdCopyBuffer per upload, submits,
        // and `vkQueueWaitIdle`s. The N uploads share one wait.
        //
        // For per-frame streaming uploads use `StagingUploader` instead
        // (see STEP 6) — it's the same idea but lifecycle-fitted to a
        // FrameRing (Reset() rewinds heads each frame, no waits, you
        // record the consuming copy into the frame's command buffer).
        //
        // After Flush the staging chunks are recycled and the
        // destination buffers contain the data — vertexBuffer is now
        // ready for binding to a draw.
        // ---------------------------------------------------------------
        using (var batch = new StagingBatch(allocator))
        {
            batch.EnqueueUpload<Vertex>(cpuVertices, in vertexBuffer);
            // Multiple Enqueues here would all flush together — e.g.
            // batch.EnqueueUpload<ushort>(indices, in indexBuffer);
            // batch.EnqueueUpload<byte>(texturePixels, in pixelStaging);
            batch.Flush(queue, cmdPool);
            Console.WriteLine($"StagingBatch flushed {vbBytes:N0} bytes into the device-local vertex buffer.");
        }

        // ---------------------------------------------------------------
        //  STEP 6 — StagingUploader: per-frame ring with no per-upload
        //           syncs.
        // ---------------------------------------------------------------
        // Quick standalone tour. A real frame loop (see HelloCube for
        // the FrameRing wiring) would call Upload once per dynamic
        // payload, record copy commands into the frame's command buffer
        // alongside other work, and submit once for the whole frame.
        // Here we just demonstrate the API and verify the bytes land.
        //
        // The pattern:
        //   1. uploader.Upload<T>(span)            — bump-copy into a
        //                                            persistent host
        //                                            chunk; returns a
        //                                            StagedUpload that
        //                                            knows its source
        //                                            buffer + offset.
        //   2. rec.CopyBuffer(staged.Source, dst,  — record the copy
        //         staged.ToCopyRegion(dstOff))      using the upload's
        //                                            offset as src.
        //   3. (frame submit happens elsewhere)
        //   4. uploader.Reset()                    — at the next frame's
        //                                            begin, rewind heads.
        //                                            Chunks stay alive,
        //                                            no VMA traffic in
        //                                            steady state.
        // ---------------------------------------------------------------
        using (var uploader = new StagingUploader(allocator))
        {
            // Tiny device-local buffer to copy into — purely to exercise
            // the API end-to-end. In a real frame it'd be your dynamic
            // vertex / uniform / instance buffer.
            using var demoDst = allocator.CreateBuffer(
                new BufferDescription { Size = 64, Usage = BufferUsage.TransferDst | BufferUsage.UniformBuffer },
                new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

            uint[] payload = [0xCAFEBABE, 0xDEADBEEF, 0x12345678, 0x87654321];
            StagedUpload staged = uploader.Upload<uint>(payload);

            // ImmediateSubmit is a one-shot record/submit/WaitIdle helper
            // — same shape as StagingBatch internally, exposed for
            // ad-hoc one-off GPU work. In a real frame loop you'd record
            // this CopyBuffer into the frame's command buffer instead.
            // Capture into locals so the ref-friendly `in` on CopyBuffer
            // doesn't try to take a reference to a property getter on the
            // captured record struct (CS8156 if you write `in staged.Source`).
            Buffer           stagingSrc = staged.Source;
            BufferCopyRegion copyRegion = staged.ToCopyRegion(dstOffset: 0);
            queue.ImmediateSubmit(cmdPool, (ref CommandRecorder rec) =>
            {
                rec.CopyBuffer(in stagingSrc, in demoDst, copyRegion);
            });

            Console.WriteLine($"StagingUploader: {payload.Length * sizeof(uint)} bytes uploaded, " +
                $"chunks={uploader.ChunkCount}, used={uploader.UsedBytes:N0} bytes (pre-Reset).");
            uploader.Reset();
            Console.WriteLine($"After Reset: chunks={uploader.ChunkCount} (kept), " +
                $"used={uploader.UsedBytes:N0} bytes (rewound).");
        }

        // ---------------------------------------------------------------
        //  STEP 7 — Pipeline + descriptor layout for the UBO.
        // ---------------------------------------------------------------
        //  Pure pipeline plumbing — has nothing to do with VMA. Skim
        //  if you're here for memory; the only allocator-relevant bit
        //  is that the descriptor will reference `tintUbo.Handle` +
        //  `tintUbo.Size`, which `BufferDescriptorWrite.Of(in Buffer)`
        //  pulls off the wrapper handle for us.
        // ---------------------------------------------------------------
        DescriptorBinding[] bindings =
        [
            new()
            {
                Slot   = 0,
                Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                Count  = 1,
                Stages = ShaderStages.Fragment,
            },
        ];
        using var setLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings       = bindings,
            // Push descriptors avoid the descriptor-pool lifecycle for
            // a transient single-set binding. The wrapper's "3D-game
            // baseline" device features turn this on whenever supported.
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

        ReadOnlySpan<VkFormat> colorFormats = stackalloc VkFormat[] { VkFormat.VK_FORMAT_R8G8B8A8_UNORM };
        using var pipeline = device.BuildGraphicsPipeline()
            .WithStages(in vMod, in fMod)
            .WithVertexInput(new VertexInputDescription { Bindings = vBindings, Attributes = vAttrs })
            .WithDynamicRendering(colorFormats)
            .WithLayout(in pipelineLayout)
            .Build();

        // ---------------------------------------------------------------
        //  STEP 8 — Record + submit the render.
        // ---------------------------------------------------------------
        //  This is normal Vulkan. The only line that touches VMA-shaped
        //  state is the `BindVertexBuffers([vertexBuffer])` — the
        //  wrapper's `Buffer` is the same handle that VMA returned, and
        //  the bind pulls `.Handle` off it directly.
        // ---------------------------------------------------------------
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.PipelineBarrier(ImageBarrier.Transition(in renderTarget,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    srcStage: Stage.TopOfPipe,             srcAccess: Access.None,
                    dstStage: Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite));

                ColorAttachment[] color = [new ColorAttachment
                {
                    View       = renderTargetView,
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

                // Push the tintUbo descriptor for this draw. The wrapper's
                // BufferDescriptorWrite.Of(in Buffer) reads `.Handle` and
                // `.Size` off the VMA-allocated buffer, so the descriptor
                // covers the entire UBO range automatically.
                DescriptorWrite[] writes =
                [
                    DescriptorWrite.Buffer(
                        binding: 0, arrayElement: 0,
                        VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                        BufferDescriptorWrite.Of(in tintUbo)),
                ];
                rec.PushDescriptorSet(VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS,
                    in pipelineLayout, set: 0, writes);

                Buffer[] vertexBuffers = [vertexBuffer];
                rec.BindVertexBuffers(0, vertexBuffers);
                rec.Draw(vertexCount: (uint)cpuVertices.Length);

                rec.EndRendering();

                // Color attachment → transfer source for the readback copy.
                rec.PipelineBarrier(ImageBarrier.Transition(in renderTarget,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    srcStage: Stage.ColorAttachmentOutput, srcAccess: Access.ColorAttachmentWrite,
                    dstStage: Stage.AllTransfer,           dstAccess: Access.TransferRead));

                // GPU image → CPU-readable buffer. The wrapper's
                // BufferImageCopy.WholeImage covers mip 0 / layer 0 of
                // the renderTarget at offset 0 of the readback buffer.
                rec.CopyImageToBuffer(in renderTarget,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    in readback,
                    BufferImageCopy.WholeImage(in renderTarget));

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

        // ---------------------------------------------------------------
        //  STEP 9 — Read pixels back through the host-visible buffer.
        // ---------------------------------------------------------------
        // The fence above guarantees the GPU is done writing the
        // readback buffer. On non-coherent memory the CPU's cache could
        // still hold stale lines for the readback range, so we
        // `Invalidate()` before reading. Coherent memory (the desktop
        // norm) makes this a no-op.
        //
        // `AsReadOnlySpan<byte>()` returns a span over the live mapped
        // pointer — zero copy, valid for the lifetime of the buffer.
        // ---------------------------------------------------------------
        readback.Invalidate();
        ReadOnlySpan<byte> pixels = readback.AsReadOnlySpan<byte>();
        PngWriter.Write(outPath, pixels, (int)Width, (int)Height);
        Console.WriteLine($"Wrote {outPath} ({Width}×{Height}, {pixels.Length:N0} bytes pre-encode).");

        // Validation summary. Drain via Volatile.Read in case the layer
        // delivered any final messages from a non-main thread between
        // the last submit and now.
        int errors   = System.Threading.Volatile.Read(ref s_validationErrors);
        int warnings = System.Threading.Volatile.Read(ref s_validationWarnings);
        Console.WriteLine($"Validation: {errors} error(s), {warnings} warning(s).");
        return errors == 0 ? 0 : 4;
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
