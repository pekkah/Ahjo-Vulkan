using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Ahjo.Vulkan;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Utilities;

namespace Ahjo.Vulkan.Samples.HelloCube;

/// <summary>
/// Windowed sample: opens a window through the SDL3 shim, builds a
/// swapchain with a matching depth attachment, uploads a textured unit
/// cube (24 vertices × 36 indices, position + UV + per-face tint), and
/// spins it in front of the camera through a push-constant MVP matrix.
/// The diffuse texture is the CC0 <c>Planks010</c> color map from
/// ambientCG, sampled through a per-frame push descriptor. Press
/// <kbd>W</kbd> to toggle wireframe, <kbd>Esc</kbd> to quit.
/// Cross-platform (Windows + Linux X11/Wayland + macOS MoltenVK).
/// </summary>
internal static unsafe class Program
{
    private const VkFormat DepthFormat   = VkFormat.VK_FORMAT_D32_SFLOAT;
    private const VkFormat TextureFormat = VkFormat.VK_FORMAT_R8G8B8A8_UNORM;

    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex
    {
        public Vector3 Position;
        public Vector2 UV;
        public Vector3 FaceTint;
        public Vertex(Vector3 position, Vector2 uv, Vector3 faceTint)
        { Position = position; UV = uv; FaceTint = faceTint; }
    }

    private struct CubePushConstants
    {
        public Matrix4x4 Mvp;
    }

    // Six face tints kept in [0.6, 1.0] so the wood-plank texture
    // detail still reads through the multiply in cube.frag.
    private static readonly Vector3 TintPosZ = new(1.00f, 0.95f, 0.85f); // warm
    private static readonly Vector3 TintNegZ = new(0.85f, 1.00f, 0.85f); // green-tinged
    private static readonly Vector3 TintPosX = new(0.85f, 0.90f, 1.00f); // cool
    private static readonly Vector3 TintNegX = new(1.00f, 1.00f, 0.75f); // yellow
    private static readonly Vector3 TintPosY = new(1.00f, 0.85f, 1.00f); // pink
    private static readonly Vector3 TintNegY = new(0.75f, 1.00f, 1.00f); // teal

    // Per-face quad: 4 vertices × 6 faces. UVs sweep 0..1 across each
    // face so the entire texture appears once per side. Winding doesn't
    // matter for visibility (cull mode is NONE) but is CCW from outside.
    private static readonly Vertex[] CubeVertices =
    [
        // +Z front
        new(new(-1, -1,  1), new(0, 1), TintPosZ),
        new(new( 1, -1,  1), new(1, 1), TintPosZ),
        new(new( 1,  1,  1), new(1, 0), TintPosZ),
        new(new(-1,  1,  1), new(0, 0), TintPosZ),
        // -Z back
        new(new( 1, -1, -1), new(0, 1), TintNegZ),
        new(new(-1, -1, -1), new(1, 1), TintNegZ),
        new(new(-1,  1, -1), new(1, 0), TintNegZ),
        new(new( 1,  1, -1), new(0, 0), TintNegZ),
        // +X right
        new(new( 1, -1,  1), new(0, 1), TintPosX),
        new(new( 1, -1, -1), new(1, 1), TintPosX),
        new(new( 1,  1, -1), new(1, 0), TintPosX),
        new(new( 1,  1,  1), new(0, 0), TintPosX),
        // -X left
        new(new(-1, -1, -1), new(0, 1), TintNegX),
        new(new(-1, -1,  1), new(1, 1), TintNegX),
        new(new(-1,  1,  1), new(1, 0), TintNegX),
        new(new(-1,  1, -1), new(0, 0), TintNegX),
        // +Y top
        new(new(-1,  1,  1), new(0, 1), TintPosY),
        new(new( 1,  1,  1), new(1, 1), TintPosY),
        new(new( 1,  1, -1), new(1, 0), TintPosY),
        new(new(-1,  1, -1), new(0, 0), TintPosY),
        // -Y bottom
        new(new(-1, -1, -1), new(0, 1), TintNegY),
        new(new( 1, -1, -1), new(1, 1), TintNegY),
        new(new( 1, -1,  1), new(1, 0), TintNegY),
        new(new(-1, -1,  1), new(0, 0), TintNegY),
    ];

    private static readonly ushort[] CubeIndices =
    [
         0,  1,  2,    0,  2,  3,
         4,  5,  6,    4,  6,  7,
         8,  9, 10,    8, 10, 11,
        12, 13, 14,   12, 14, 15,
        16, 17, 18,   16, 18, 19,
        20, 21, 22,   20, 22, 23,
    ];

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

        string baseDir    = AppContext.BaseDirectory;
        string shadersDir = Path.Combine(baseDir, "Shaders");
        string vertSpv    = Path.Combine(shadersDir, "cube.vert.spv");
        string fragSpv    = Path.Combine(shadersDir, "cube.frag.spv");
        string crateImg   = Path.Combine(baseDir, "Textures", "crate.png");
        if (!File.Exists(vertSpv) || !File.Exists(fragSpv))
        {
            Console.Error.WriteLine($"Missing compiled shaders. Expected:\n  {vertSpv}\n  {fragSpv}");
            return 2;
        }
        if (!File.Exists(crateImg))
        {
            Console.Error.WriteLine($"Missing texture: {crateImg}");
            return 2;
        }

        using var window = new SdlWindow("Ahjo.Vulkan — HelloCube (W: wireframe • Esc: quit)", 1024, 768,
            hidden: false, resizable: true);

        Utf8Name[] instanceExts = SdlWindow.GetRequiredVulkanInstanceExtensions();
        // Validation enabled — same standing regression check pattern
        // as HelloTriangle / HelloVmaWindowed. Findings hit stderr at
        // WARN/ERROR severity.
        using var instance = Instance.Create(new InstanceDescription
        {
            Extensions       = instanceExts,
            EnableValidation = true,
            DebugCallback    = OnValidationMessage,
        });

        using var surface = window.CreateVulkanSurface(instance);
        using var device  = CreatePresentDevice(instance, in surface, out uint family, out bool wireframeSupported);

        var swapDesc = new SwapchainDescription
        {
            Surface = surface,
            Width   = window.Width,
            Height  = window.Height,
        };
        using var swap = new Swapchain(device, in swapDesc);

        // ---- Vertex + index buffers (host-visible mapped — small payload). ----
        ulong vbBytes = (ulong)(CubeVertices.Length * sizeof(Vertex));
        ulong ibBytes = (ulong)(CubeIndices.Length  * sizeof(ushort));

        using var vbo = device.Allocator.CreateBuffer(
            new BufferDescription { Size = vbBytes, Usage = BufferUsage.VertexBuffer },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });
        CubeVertices.AsSpan().CopyTo(vbo.AsSpan<Vertex>());
        vbo.Flush();

        using var ibo = device.Allocator.CreateBuffer(
            new BufferDescription { Size = ibBytes, Usage = BufferUsage.IndexBuffer },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });
        CubeIndices.AsSpan().CopyTo(ibo.AsSpan<ushort>());
        ibo.Flush();

        // ---- Texture: load PNG, allocate device image with mips, upload + GenerateMips. ----
        // Diagnostic: bypass the PNG decoder with a synthetic 64×64 magenta-
        // and-yellow checkerboard. If this shows up but the PNG-loaded one
        // doesn't, the issue is in PngReader; if neither shows up, the
        // pipeline (sampler / descriptor / barriers / GenerateMips) is the
        // failure point.
        byte[] pixels = PngReader.LoadRgba8(crateImg, out int texW, out int texH);
        uint mipLevels = (uint)BitOperations.Log2((uint)Math.Max(texW, texH)) + 1;

        using var texImage = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType   = VkImageType.VK_IMAGE_TYPE_2D,
                Format      = TextureFormat,
                Width       = (uint)texW, Height = (uint)texH, Depth = 1,
                MipLevels   = mipLevels, ArrayLayers = 1,
                Samples     = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling      = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                // GenerateMips blits mip i-1 → mip i, so the image needs
                // both TransferSrc (read-from-prior-mip) and TransferDst
                // (write-into-next-mip) in addition to Sampled for the
                // shader-read use.
                Usage       = ImageUsage.Sampled | ImageUsage.TransferDst | ImageUsage.TransferSrc,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using var texView = texImage.CreateView(device, new ImageViewDescription
        {
            ViewType       = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect         = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            BaseMipLevel   = 0, LevelCount = mipLevels,
            BaseArrayLayer = 0, LayerCount = 1,
        });

        UploadAndMipTexture(device, in texImage, pixels, family);

        var samplerDesc = new SamplerDescription
        {
            MagFilter        = VkFilter.VK_FILTER_LINEAR,
            MinFilter        = VkFilter.VK_FILTER_LINEAR,
            MipmapMode       = VkSamplerMipmapMode.VK_SAMPLER_MIPMAP_MODE_LINEAR,
            AddressModeU     = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_REPEAT,
            AddressModeV     = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_REPEAT,
            AddressModeW     = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_REPEAT,
            // Anisotropy is part of the wrapper's queried "3D-game baseline"
            // and is on whenever the device advertises it (the CreateSampler
            // path clamps MaxAnisotropy to the device's reported limit).
            AnisotropyEnable = true,
            MaxAnisotropy    = 16f,
            MinLod           = 0f,
            MaxLod           = mipLevels,
            BorderColor      = VkBorderColor.VK_BORDER_COLOR_FLOAT_OPAQUE_BLACK,
        };
        using var sampler = device.CreateSampler(in samplerDesc);

        // ---- Descriptor set layout (push descriptor: no pool needed). ----
        DescriptorBinding[] textureBindings =
        [
            new()
            {
                Slot   = 0,
                Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
                Count  = 1,
                Stages = ShaderStages.Fragment,
            },
        ];
        using var textureLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings       = textureBindings,
            PushDescriptor = true,
        });

        // ---- Pipeline ----
        using var vertBlob = SpirvBlob.Load(vertSpv);
        using var fragBlob = SpirvBlob.Load(fragSpv);
        using var vMod  = device.CreateShaderModule(vertBlob.Words);
        using var fMod  = device.CreateShaderModule(fragBlob.Words);

        DescriptorSetLayout[] setLayouts = [textureLayout];
        PushConstantRange[]   pushRanges = [PushConstantRange.For<CubePushConstants>(ShaderStages.Vertex)];
        using var pipeLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts         = setLayouts,
            PushConstantRanges = pushRanges,
        });

        VertexBindingDescription[] vBindings =
        [
            new() { Slot = 0, Stride = (uint)sizeof(Vertex), InputRate = VkVertexInputRate.VK_VERTEX_INPUT_RATE_VERTEX },
        ];
        VertexAttributeDescription[] vAttrs =
        [
            new() { Location = 0, Binding = 0, Format = VkFormat.VK_FORMAT_R32G32B32_SFLOAT, Offset = 0 },
            new() { Location = 1, Binding = 0, Format = VkFormat.VK_FORMAT_R32G32_SFLOAT,    Offset = (uint)sizeof(Vector3) },
            new() { Location = 2, Binding = 0, Format = VkFormat.VK_FORMAT_R32G32B32_SFLOAT, Offset = (uint)(sizeof(Vector3) + sizeof(Vector2)) },
        ];

        // ---- Pipeline cache. The driver merges newly-compiled pipelines
        //      into the cache as we build; we save it back on shutdown so
        //      the next run skips the SPIR-V→ISA pass for both pipelines. ----
        string cachePath = Path.Combine(baseDir, "hellocube.pipeline-cache");
        var pipelineCache = device.LoadOrCreatePipelineCache(cachePath);

        VkFormat swapFormat = swap.Format;
        ReadOnlySpan<VkFormat> colorFormats = stackalloc VkFormat[] { swapFormat };

        // Both pipelines share the layout and shader stages; only the
        // rasterizer's polygon mode differs. The pipeline cache makes the
        // second build essentially free, and saves ISA for the next run.
        using var solidPipeline = device.BuildGraphicsPipeline()
            .WithStages(in vMod, in fMod)
            .WithVertexInput(new VertexInputDescription { Bindings = vBindings, Attributes = vAttrs })
            .WithDynamicRendering(colorFormats, depthFormat: DepthFormat)
            .WithDepthStencil(testEnable: true, writeEnable: true, compareOp: VkCompareOp.VK_COMPARE_OP_LESS)
            .WithLayout(in pipeLayout)
            .WithCache(in pipelineCache)
            .Build();

        // Wireframe pipeline only when fillModeNonSolid is on the device —
        // the wrapper's "3D-game baseline" enables it whenever supported,
        // so this lights up on practically every desktop GPU but
        // gracefully skips on the rare device that lacks it.
        GraphicsPipeline wirePipeline = default;
        if (wireframeSupported)
        {
            wirePipeline = device.BuildGraphicsPipeline()
                .WithStages(in vMod, in fMod)
                .WithVertexInput(new VertexInputDescription { Bindings = vBindings, Attributes = vAttrs })
                .WithDynamicRendering(colorFormats, depthFormat: DepthFormat)
                .WithDepthStencil(testEnable: true, writeEnable: true, compareOp: VkCompareOp.VK_COMPARE_OP_LESS)
                .WithRasterization(polygonMode: VkPolygonMode.VK_POLYGON_MODE_LINE)
                .WithLayout(in pipeLayout)
                .WithCache(in pipelineCache)
                .Build();
        }
        else
        {
            Console.WriteLine("fillModeNonSolid not supported on this device — wireframe toggle disabled.");
        }

        DepthBuffer depth = DepthBuffer.Create(device, swap.Extent.width, swap.Extent.height);

        using var ring = new FrameRing(device, framesInFlight: 2, queueFamily: family);
        var queue = device.GetQueue(family, 0);

        Console.WriteLine($"Swapchain: {swap.Format} {swap.Extent.width}x{swap.Extent.height}, {swap.ImageCount} images, {swap.PresentMode}");
        Console.WriteLine($"Texture:   {texW}x{texH} ({mipLevels} mips), {pixels.Length:N0} bytes pre-upload.");

        var clock = Stopwatch.StartNew();
        ulong frame = 0;
        bool  wireframe = false;
        try
        {
            while (!window.ShouldClose)
            {
                window.PumpEvents();
                if (window.ShouldClose) break;
                if (window.ConsumeWireframeToggle() && wireframeSupported)
                {
                    wireframe = !wireframe;
                    Console.WriteLine($"Wireframe: {(wireframe ? "on" : "off")}");
                }

                if (window.ConsumeResize() || swap.Extent.width != window.Width || swap.Extent.height != window.Height)
                {
                    device.WaitIdle();
                    swap.Recreate(new SwapchainDescription
                    {
                        Surface = surface,
                        Width   = window.Width,
                        Height  = window.Height,
                    });
                    depth.Dispose();
                    depth = DepthBuffer.Create(device, swap.Extent.width, swap.Extent.height);
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
                    depth.Dispose();
                    depth = DepthBuffer.Create(device, swap.Extent.width, swap.Extent.height);
                    ring.RecycleStaleAcquireSemaphores();
                    continue;
                }
                if (acq != AcquireResult.Success)
                {
                    Console.Error.WriteLine($"AcquireNextImage: {acq}");
                    continue;
                }

                ImageView swapView = swap.ImageViews[(int)imageIndex];

                float t       = (float)clock.Elapsed.TotalSeconds;
                float aspect  = swap.Extent.height == 0 ? 1f : (float)swap.Extent.width / swap.Extent.height;
                var   pushPC  = new CubePushConstants { Mvp = BuildMvp(t, aspect) };

                var rec = fc.CommandBuffers.Begin();
                try
                {
                    RecordSwapchainBarrier(ref rec, swap, imageIndex,
                        from: VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                        to:   VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                        srcStage: Stage.TopOfPipe,             srcAccess: Access.None,
                        dstStage: Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite);

                    rec.PipelineBarrier(ImageBarrier.Transition(in depth.Image,
                        from: VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                        to:   VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL,
                        srcStage: Stage.EarlyFragmentTests | Stage.LateFragmentTests, srcAccess: Access.None,
                        dstStage: Stage.EarlyFragmentTests | Stage.LateFragmentTests, dstAccess: Access.DepthStencilAttachmentWrite,
                        aspect:  VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT));

                    ColorAttachment[] color = [new ColorAttachment
                    {
                        View       = swapView,
                        Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                        LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                        StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                        ClearColor = ClearColor(0.05f, 0.07f, 0.10f, 1.0f),
                    }];
                    var depthAttachment = new DepthAttachment
                    {
                        View       = depth.View,
                        Layout     = VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL,
                        LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                        StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_DONT_CARE,
                        ClearDepth = 1.0f,
                    };

                    rec.BeginRendering(new RenderingInfo
                    {
                        RenderArea       = new VkRect2D { extent = swap.Extent },
                        LayerCount       = 1,
                        ColorAttachments = color,
                        DepthAttachment  = depthAttachment,
                    });
                    rec.SetViewport(new VkViewport
                    {
                        x = 0, y = 0,
                        width  = swap.Extent.width,
                        height = swap.Extent.height,
                        minDepth = 0, maxDepth = 1,
                    });
                    rec.SetScissor(new VkRect2D { extent = swap.Extent });

                    using (var drawScope = rec.LabelScope(wireframe ? "draw-cube-wire"u8 : "draw-cube"u8))
                    {
                        if (wireframe) rec.BindPipeline(in wirePipeline);
                        else           rec.BindPipeline(in solidPipeline);

                        DescriptorWrite[] writes =
                        [
                            DescriptorWrite.CombinedImageSampler(
                                binding: 0, arrayElement: 0,
                                ImageDescriptorWrite.Of(in sampler, in texView,
                                    VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL)),
                        ];
                        rec.PushDescriptorSet(VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS,
                            in pipeLayout, set: 0, writes);

                        Buffer[] vertexBuffers = [vbo];
                        rec.BindVertexBuffers(0, vertexBuffers);
                        rec.BindIndexBuffer(in ibo, 0, VkIndexType.VK_INDEX_TYPE_UINT16);
                        rec.PushConstants(in pipeLayout, ShaderStages.Vertex, in pushPC);
                        rec.DrawIndexed((uint)CubeIndices.Length);
                    }
                    rec.EndRendering();

                    RecordSwapchainBarrier(ref rec, swap, imageIndex,
                        from: VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                        to:   VkImageLayout.VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
                        srcStage: Stage.ColorAttachmentOutput, srcAccess: Access.ColorAttachmentWrite,
                        dstStage: Stage.BottomOfPipe,          dstAccess: Access.None);

                    // Swapchain-aware submit + matching present pull the
                    // swapchain's per-image RenderingDone semaphore — see
                    // issue #89 for why per-slot signaling was wrong.
                    fc.Submit(queue, ref rec, swap, imageIndex);
                }
                finally { rec.Dispose(); }

                var pres = swap.Present(queue, imageIndex);
                if (pres is AcquireResult.OutOfDate or AcquireResult.Suboptimal)
                {
                    device.WaitIdle();
                    swap.Recreate(new SwapchainDescription
                    {
                        Surface = surface,
                        Width   = window.Width,
                        Height  = window.Height,
                    });
                    depth.Dispose();
                    depth = DepthBuffer.Create(device, swap.Extent.width, swap.Extent.height);
                    ring.RecycleStaleAcquireSemaphores();
                }

                frame++;
                if (frame >= maxFrames) break;
            }

            device.WaitIdle();
            Console.WriteLine($"Rendered {frame} frames.");
            return 0;
        }
        finally
        {
            depth.Dispose();
            if (!wirePipeline.IsNull) wirePipeline.Dispose();
            // Save the cache before disposing it. WaitIdle above ensures
            // every pipeline-build the driver might still be flushing has
            // landed in the cache before we serialize.
            try { pipelineCache.Save(cachePath); }
            catch (Exception ex)
            { Console.Error.WriteLine($"PipelineCache.Save failed: {ex.Message}"); }
            pipelineCache.Dispose();
        }
    }

    /// <summary>
    /// One-shot upload: stage the PNG pixels through a host-visible buffer,
    /// blit-mip-chain the texture, and leave every mip in
    /// <c>VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL</c> ready for sampling.
    /// </summary>
    private static unsafe void UploadAndMipTexture(
        Device       device,
        in Image     image,
        byte[]       pixelsRgba8,
        uint         queueFamily)
    {
        using var staging = device.Allocator.CreateBuffer(
            new BufferDescription { Size = (ulong)pixelsRgba8.Length, Usage = BufferUsage.TransferSrc },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });
        pixelsRgba8.AsSpan().CopyTo(staging.AsSpan<byte>());
        staging.Flush();

        using var pool = new CommandBufferPool(device, queueFamily);
        var queue = device.GetQueue(queueFamily, 0);

        Image localImage = image;
        queue.ImmediateSubmit(pool, (ref CommandRecorder rec) =>
        {
            using var labelScope = rec.LabelScope("upload-crate"u8);
            // 1. UNDEFINED → TRANSFER_DST_OPTIMAL on mip 0 only — GenerateMips
            //    will rotate the rest of the chain itself.
            rec.PipelineBarrier(new ImageBarrier
            {
                Image               = (nint)localImage.Handle,
                SrcStage            = Stage.TopOfPipe, SrcAccess = Access.None,
                DstStage            = Stage.Copy,      DstAccess = Access.TransferWrite,
                OldLayout           = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                NewLayout           = VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                SrcQueueFamilyIndex = ImageBarrier.QueueFamilyIgnored,
                DstQueueFamilyIndex = ImageBarrier.QueueFamilyIgnored,
                Aspect              = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                BaseMipLevel        = 0, LevelCount = 1,
                BaseArrayLayer      = 0, LayerCount = 1,
            });

            rec.CopyBufferToImage(in staging, in localImage,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                BufferImageCopy.WholeImage(in localImage));

            rec.GenerateMips(in localImage,
                finalLayout: VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);
        });
    }

    /// <summary>
    /// Builds a model-view-projection matrix for the cube. System.Numerics
    /// stores matrices row-major; GLSL reads a 64-byte <c>mat4</c> push
    /// constant column-major. Pushing the .NET bytes directly therefore
    /// hands GLSL the transpose of the row-major matrix — which is exactly
    /// the column-vector form GLSL multiplies by <c>vec4(pos, 1)</c>. Y is
    /// negated on the projection so world-space +Y maps to the top of the
    /// framebuffer after Vulkan's +Y-down clip space.
    /// </summary>
    private static Matrix4x4 BuildMvp(float seconds, float aspect)
    {
        var model =
            Matrix4x4.CreateRotationY(seconds * 0.6f) *
            Matrix4x4.CreateRotationX(seconds * 0.4f);

        var view = Matrix4x4.CreateLookAt(
            cameraPosition: new Vector3(0, 0, -3.5f),
            cameraTarget:   Vector3.Zero,
            cameraUpVector: Vector3.UnitY);

        var proj = Matrix4x4.CreatePerspectiveFieldOfView(
            fieldOfView: MathF.PI / 3f,
            aspectRatio: aspect,
            nearPlaneDistance: 0.1f,
            farPlaneDistance:  100f);
        proj.M22 *= -1f;

        return model * view * proj;
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

    private static Device CreatePresentDevice(
        Instance      instance,
        in Surface    surface,
        out uint      family,
        out bool      fillModeNonSolidSupported)
    {
        Surface local      = surface;
        uint    chosen     = uint.MaxValue;
        bool    nonSolid   = false;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (!info.QueueFamilies[i].SupportsGraphics) continue;
                if (info.Device.SupportsPresent(info.QueueFamilies[i].Index, in local))
                {
                    chosen   = info.QueueFamilies[i].Index;
                    nonSolid = info.Features.fillModeNonSolid != 0;
                    return true;
                }
            }
            return false;
        });
        family                    = chosen;
        fillModeNonSolidSupported = nonSolid;

        Utf8Name[] deviceExts = [VulkanExtensions.KhrSwapchain];
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues     = [new QueueRequest(family, count: 1, priority: 1.0f)],
            Extensions = deviceExts,
        });
    }

    /// <summary>
    /// Pairs a depth <see cref="Image"/> with its <see cref="ImageView"/>
    /// so the resize path can dispose them as a unit.
    /// </summary>
    private struct DepthBuffer : IDisposable
    {
        public Image     Image;
        public ImageView View;

        public static DepthBuffer Create(Device device, uint width, uint height)
        {
            Image image = device.Allocator.CreateImage(
                new ImageDescription
                {
                    ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                    Format        = DepthFormat,
                    Width         = width, Height = height, Depth = 1,
                    MipLevels     = 1, ArrayLayers = 1,
                    Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                    Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                    Usage         = ImageUsage.DepthStencilAttachment,
                    InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                },
                new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

            ImageView view = image.CreateView(device, new ImageViewDescription
            {
                ViewType       = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
                Aspect         = VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT,
                BaseMipLevel   = 0, LevelCount = 1,
                BaseArrayLayer = 0, LayerCount = 1,
            });

            return new DepthBuffer { Image = image, View = view };
        }

        public void Dispose()
        {
            View.Dispose();
            Image.Dispose();
        }
    }
}
