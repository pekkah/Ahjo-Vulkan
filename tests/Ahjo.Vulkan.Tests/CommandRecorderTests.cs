using System.IO;
using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class CommandRecorderTests
{
    [Fact]
    public void Default_Recorder_IsNull()
    {
        CommandRecorder rec = default;
        Assert.True(rec.IsNull);
        rec.Dispose();
    }

    [Fact]
    public void Recorder_End_IsIdempotent()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);
        using var pool     = new CommandBufferPool(device, family);

        using var rec = pool.Begin();
        rec.End();
        rec.End();  // no throw on repeat
    }

    [Fact]
    public void ComputeDispatch_FillBuffer_RoundTrips()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipUnless(File.Exists(FillSpvPath), $"fill.comp.spv missing at {FillSpvPath}.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        // ---- Resources ----
        using var blob   = SpirvBlob.Load(FillSpvPath);
        using var module = device.CreateShaderModule(blob.Words);

        DescriptorBinding[] bindings =
        [
            new DescriptorBinding
            {
                Slot = 0, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                Count = 1, Stages = ShaderStages.Compute,
            },
        ];
        using var setLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings       = bindings,
            PushDescriptor = true,
        });

        DescriptorSetLayout[] layouts = [setLayout];
        PushConstantRange[]   ranges  = [PushConstantRange.For<PushBlock>(ShaderStages.Compute)];
        using var pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts         = layouts,
            PushConstantRanges = ranges,
        });

        using var pipeline = device.BuildComputePipeline()
            .WithShader(in module)
            .WithLayout(in pipelineLayout)
            .Build();

        const uint Count = 256;
        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = Count * sizeof(uint),
                Usage = BufferUsage.StorageBuffer,
            },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        // ---- Descriptor wiring (typed push-descriptor template) ----
        using var template = pipelineLayout.CreatePushDescriptorTemplate<FillDescriptors>(
            set: 0, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE, bindings);

        // ---- Record + submit ----
        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.BindPipeline(in pipeline);
                var writes = new FillDescriptors
                {
                    Out = BufferDescriptorWrite.Of(in buffer),
                };
                rec.PushDescriptors(in template, in pipelineLayout, in writes);
                var pc = new PushBlock { Count = Count };
                rec.PushConstants(in pipelineLayout, ShaderStages.Compute, in pc);
                rec.Dispatch(groupCountX: (Count + 63) / 64);

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        // ---- Verify ----
        ReadOnlySpan<uint> data = buffer.AsReadOnlySpan<uint>();
        for (int i = 0; i < (int)Count; i++)
            Assert.Equal((uint)i, data[i]);
    }

    [Fact]
    public void DispatchIndirect_FillBuffer_RoundTrips()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipUnless(File.Exists(FillSpvPath), $"fill.comp.spv missing at {FillSpvPath}.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var blob   = SpirvBlob.Load(FillSpvPath);
        using var module = device.CreateShaderModule(blob.Words);

        DescriptorBinding[] bindings =
        [
            new DescriptorBinding
            {
                Slot = 0, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                Count = 1, Stages = ShaderStages.Compute,
            },
        ];
        using var setLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings       = bindings,
            PushDescriptor = true,
        });

        DescriptorSetLayout[] layouts = [setLayout];
        PushConstantRange[]   ranges  = [PushConstantRange.For<PushBlock>(ShaderStages.Compute)];
        using var pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts         = layouts,
            PushConstantRanges = ranges,
        });

        using var pipeline = device.BuildComputePipeline()
            .WithShader(in module)
            .WithLayout(in pipelineLayout)
            .Build();

        const uint Count = 256;
        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = Count * sizeof(uint),
                Usage = BufferUsage.StorageBuffer,
            },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        // Indirect dispatch parameters live in a host-visible buffer so the
        // CPU can write the (x, y, z) group counts the GPU then reads.
        using var indirect = device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = (ulong)sizeof(VkDispatchIndirectCommand),
                Usage = BufferUsage.IndirectBuffer,
            },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });
        indirect.AsSpan<VkDispatchIndirectCommand>()[0] = new VkDispatchIndirectCommand
        {
            x = (Count + 63) / 64, y = 1, z = 1,
        };

        using var template = pipelineLayout.CreatePushDescriptorTemplate<FillDescriptors>(
            set: 0, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE, bindings);

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.BindPipeline(in pipeline);
                var writes = new FillDescriptors
                {
                    Out = BufferDescriptorWrite.Of(in buffer),
                };
                rec.PushDescriptors(in template, in pipelineLayout, in writes);
                var pc = new PushBlock { Count = Count };
                rec.PushConstants(in pipelineLayout, ShaderStages.Compute, in pc);
                rec.DispatchIndirect(in indirect, offset: 0);

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        ReadOnlySpan<uint> data = buffer.AsReadOnlySpan<uint>();
        for (int i = 0; i < (int)Count; i++)
            Assert.Equal((uint)i, data[i]);
    }

    [Fact]
    public void BindAndDrawIndexed_RecordsWithoutThrow()
    {
        // Recording-only smoke test for the new bind / indexed-draw / draw-indirect
        // surface. Real submit + pixel readback for indexed and indirect draws is
        // HelloTriangle's job (#25 follow-on); this just proves the wrapper
        // dispatches the right vkCmd* and accepts our parameter shapes.
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipUnless(File.Exists(VertSpvPath), $"triangle.vert.spv missing at {VertSpvPath}.");
        Assert.SkipUnless(File.Exists(FragSpvPath), $"triangle.frag.spv missing at {FragSpvPath}.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType   = VkImageType.VK_IMAGE_TYPE_2D,
                Format      = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width       = 64, Height = 64, Depth = 1,
                MipLevels   = 1, ArrayLayers = 1,
                Samples     = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling      = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage       = ImageUsage.ColorAttachment,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
        using var view = image.CreateView(device, new ImageViewDescription
        {
            ViewType = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect   = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            BaseMipLevel = 0, LevelCount = 1, BaseArrayLayer = 0, LayerCount = 1,
        });

        using var vBlob = SpirvBlob.Load(VertSpvPath);
        using var fBlob = SpirvBlob.Load(FragSpvPath);
        using var vMod  = device.CreateShaderModule(vBlob.Words);
        using var fMod  = device.CreateShaderModule(fBlob.Words);
        using var layout = device.CreatePipelineLayout(default);
        ReadOnlySpan<VkFormat> colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];
        using var pipeline = device.BuildGraphicsPipeline()
            .WithStages(in vMod, in fMod)
            .WithDynamicRendering(colorFormats)
            .WithLayout(in layout)
            .Build();

        // Matched-size dummy buffers so DescriptorSet validation has a
        // chance to flag obvious shape errors. Vertex / index / indirect
        // contents are unused since we don't submit.
        using var vb = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 64,                                 Usage = BufferUsage.VertexBuffer },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
        using var ib = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 64,                                 Usage = BufferUsage.IndexBuffer },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
        using var indirectDraw = device.Allocator.CreateBuffer(
            new BufferDescription { Size = (ulong)sizeof(VkDrawIndirectCommand),        Usage = BufferUsage.IndirectBuffer },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
        using var indirectIndexed = device.Allocator.CreateBuffer(
            new BufferDescription { Size = (ulong)sizeof(VkDrawIndexedIndirectCommand), Usage = BufferUsage.IndirectBuffer },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using var cmdPool = new CommandBufferPool(device, family);
        using var rec = cmdPool.Begin();

        rec.PipelineBarrier(ImageBarrier.Transition(
            in image,
            from:      VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            to:        VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
            srcStage:  Stage.TopOfPipe,             srcAccess: Access.None,
            dstStage:  Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite));

        ColorAttachment[] color = [new ColorAttachment
        {
            View       = view,
            Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
            LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
            StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
            ClearColor = ClearColor(0, 0, 0, 1),
        }];

        rec.BeginRendering(new RenderingInfo
        {
            RenderArea       = new VkRect2D { extent = new VkExtent2D { width = 64, height = 64 } },
            LayerCount       = 1,
            ColorAttachments = color,
        });
        rec.SetViewport(new VkViewport { x = 0, y = 0, width = 64, height = 64, minDepth = 0, maxDepth = 1 });
        rec.SetScissor(new VkRect2D { extent = new VkExtent2D { width = 64, height = 64 } });
        rec.BindPipeline(in pipeline);

        // Default-offset path: omit the offsets span entirely.
        Buffer[] vbs = [vb];
        rec.BindVertexBuffers(firstBinding: 0, vbs);
        // Explicit-offset path: same length as buffers.
        ulong[] offsets = [0ul];
        rec.BindVertexBuffers(firstBinding: 0, vbs, offsets);
        rec.BindIndexBuffer(in ib, offset: 0, VkIndexType.VK_INDEX_TYPE_UINT16);

        rec.DrawIndexed(indexCount: 3, instanceCount: 2, firstIndex: 0, vertexOffset: 0, firstInstance: 0);
        rec.DrawIndirect(in indirectDraw, offset: 0, drawCount: 1, stride: (uint)sizeof(VkDrawIndirectCommand));
        rec.DrawIndexedIndirect(in indirectIndexed, offset: 0, drawCount: 1, stride: (uint)sizeof(VkDrawIndexedIndirectCommand));

        rec.EndRendering();
        rec.End();
    }

    [Fact]
    public void BindVertexBuffers_RejectsMismatchedOffsets()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);
        using var pool     = new CommandBufferPool(device, family);

        using var vb = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 64, Usage = BufferUsage.VertexBuffer },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using var rec = pool.Begin();
        Buffer[] vbs    = [vb];
        ulong[]  twoOff = [0ul, 0ul];
        // CommandRecorder is a ref struct and can't be captured by a lambda;
        // assert by catching directly.
        Exception? caught = null;
        try { rec.BindVertexBuffers(0, vbs, twoOff); }
        catch (Exception ex) { caught = ex; }
        Assert.IsType<ArgumentException>(caught);
    }

    [Fact]
    public void GraphicsRecording_BeginEndRender_NoValidationErrors()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipUnless(File.Exists(VertSpvPath), $"triangle.vert.spv missing at {VertSpvPath}.");
        Assert.SkipUnless(File.Exists(FragSpvPath), $"triangle.frag.spv missing at {FragSpvPath}.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        // 256x256 RGBA8 image as the render target. We stage the layout
        // transition with a basic vkCmdPipelineBarrier inline (the typed
        // wrapper lands with #18); the test only verifies we can record
        // a complete BeginRendering/Draw/EndRendering pass without
        // submitting (real submit + readback is the HelloTriangle
        // integration test in #25).
        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType   = VkImageType.VK_IMAGE_TYPE_2D,
                Format      = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width       = 256, Height = 256, Depth = 1,
                MipLevels   = 1, ArrayLayers = 1,
                Samples     = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling      = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage       = ImageUsage.ColorAttachment | ImageUsage.TransferSrc,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using var view = image.CreateView(device, new ImageViewDescription
        {
            ViewType = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect   = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            BaseMipLevel = 0, LevelCount = 1, BaseArrayLayer = 0, LayerCount = 1,
        });

        using var vBlob = SpirvBlob.Load(VertSpvPath);
        using var fBlob = SpirvBlob.Load(FragSpvPath);
        using var vMod  = device.CreateShaderModule(vBlob.Words);
        using var fMod  = device.CreateShaderModule(fBlob.Words);
        using var layout = device.CreatePipelineLayout(default);

        ReadOnlySpan<VkFormat> colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];
        using var pipeline = device.BuildGraphicsPipeline()
            .WithStages(in vMod, in fMod)
            .WithDynamicRendering(colorFormats)
            .WithLayout(in layout)
            .Build();

        using var cmdPool = new CommandBufferPool(device, family);
        using var rec = cmdPool.Begin();

        rec.PipelineBarrier(ImageBarrier.Transition(
            in image,
            from:      VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            to:        VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
            srcStage:  Stage.TopOfPipe,             srcAccess: Access.None,
            dstStage:  Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite));

        ColorAttachment[] color = [new ColorAttachment
        {
            View       = view,
            Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
            LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
            StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
            ClearColor = ClearColor(0.1f, 0.2f, 0.3f, 1.0f),
        }];

        rec.BeginRendering(new RenderingInfo
        {
            RenderArea       = new VkRect2D { extent = new VkExtent2D { width = 256, height = 256 } },
            LayerCount       = 1,
            ColorAttachments = color,
        });
        rec.SetViewport(new VkViewport { x = 0, y = 0, width = 256, height = 256, minDepth = 0, maxDepth = 1 });
        rec.SetScissor(new VkRect2D { extent = new VkExtent2D { width = 256, height = 256 } });
        rec.BindPipeline(in pipeline);
        rec.Draw(vertexCount: 3);
        rec.EndRendering();
        // Don't submit — readback path is HelloTriangle's job (#25). The
        // test simply proves we can record a well-formed dynamic-rendering
        // pass through the wrapper.
        rec.End();
    }

    [Fact]
    public void PushConstants_64ByteStruct_PassesValidation()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,          "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer, "Validation layer not installed.");

        var errors = new List<DebugMessage>();
        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion       = VulkanVersion.V1_4,
            EnableValidation = true,
            DebugCallback    = m =>
            {
                if ((m.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0)
                    lock (errors) errors.Add(m);
            },
        });

        using var device = CreateGraphicsDevice(instance, out uint family);

        PushConstantRange[] ranges = [PushConstantRange.For<PushBlock64>(ShaderStages.Compute)];
        using var pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            PushConstantRanges = ranges,
        });

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                var pc = new PushBlock64();
                for (int i = 0; i < 16; i++) pc[i] = (uint)(i * 7 + 1);
                rec.PushConstants(in pipelineLayout, ShaderStages.Compute, in pc);

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        Assert.Equal(64, Unsafe.SizeOf<PushBlock64>());
        lock (errors)
            Assert.True(errors.Count == 0,
                "Validation errors recorded: " + string.Join("; ", errors.ConvertAll(e => e.Message)));
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

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PushBlock { public uint Count; }

    // 64 bytes — exercises the largest realistic single-stage push-constants
    // payload (Vulkan guarantees ≥ 128 across all stages combined).
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 64)]
    private struct PushBlock64
    {
        private uint _w0;
        public uint this[int i]
        {
            get { fixed (uint* p = &_w0) return p[i]; }
            set { fixed (uint* p = &_w0) p[i] = value; }
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct FillDescriptors { public BufferDescriptorWrite Out; }

    private static string FillSpvPath =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", "fill.comp.spv");

    private static string VertSpvPath =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", "triangle.vert.spv");

    private static string FragSpvPath =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", "triangle.frag.spv");

    private static Device CreateGraphicsDevice(Instance instance, out uint family)
    {
        uint f = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
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
        family = f;
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }
}
