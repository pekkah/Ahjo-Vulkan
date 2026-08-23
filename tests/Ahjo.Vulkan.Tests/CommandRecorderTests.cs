using System.IO;
using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
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
        TestGate.RequireDriver();

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
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");
        TestGate.RequireSpirv(FillSpvPath);

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
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");
        TestGate.RequireSpirv(FillSpvPath);

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
        TestGate.RequireDriver();
        TestGate.RequireSpirv(VertSpvPath);
        TestGate.RequireSpirv(FragSpvPath);

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
        using var drawCountBuffer = device.Allocator.CreateBuffer(
            new BufferDescription { Size = sizeof(uint),                                Usage = BufferUsage.IndirectBuffer },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using var cmdPool = new CommandBufferPool(device, family);
        using var rec = cmdPool.Begin();

        rec.PipelineBarrier(ImageBarrier.Transition(
            in image,
            from:      VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            to:        VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
            srcStage:  Stage.TopOfPipe,             srcAccess: Access.None,
            dstStage:  Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite));

        ReadOnlySpan<ColorAttachment> color = [new ColorAttachment
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
        rec.DrawIndirectCount(
            in indirectDraw, offset: 0,
            in drawCountBuffer, countBufferOffset: 0,
            maxDrawCount: 1, stride: (uint)sizeof(VkDrawIndirectCommand));
        rec.DrawIndexedIndirect(in indirectIndexed, offset: 0, drawCount: 1, stride: (uint)sizeof(VkDrawIndexedIndirectCommand));
        rec.DrawIndexedIndirectCount(
            in indirectIndexed, offset: 0,
            in drawCountBuffer, countBufferOffset: 0,
            maxDrawCount: 1, stride: (uint)sizeof(VkDrawIndexedIndirectCommand));

        rec.EndRendering();
        rec.End();
    }

    [Fact]
    public void DrawIndexed_Instanced_RendersTriangle()
    {
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");
        TestGate.RequireSpirv(VertSpvPath);
        TestGate.RequireSpirv(FragSpvPath);

        // Closes acceptance #1 of issue #45. triangle.vert hard-codes positions
        // by gl_VertexIndex, so an index buffer of [0,1,2] paired with
        // DrawIndexed(3) produces the same triangle a plain Draw(3) would,
        // and instanceCount=2 redraws on top — the fragment shader's constant
        // white means we just need to count any non-clear pixel to prove the
        // indexed + instanced parameters reached vkCmdDrawIndexed.
        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        const uint W = 64, H = 64;
        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType   = VkImageType.VK_IMAGE_TYPE_2D,
                Format      = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width       = W, Height = H, Depth = 1,
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

        using var index = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 3 * sizeof(ushort), Usage = BufferUsage.IndexBuffer },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });
        Span<ushort> idx = index.AsSpan<ushort>();
        idx[0] = 0; idx[1] = 1; idx[2] = 2;

        const uint Bytes = W * H * 4;
        using var readback = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Bytes, Usage = BufferUsage.TransferDst },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

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

                ReadOnlySpan<ColorAttachment> color = [new ColorAttachment
                {
                    View       = view,
                    Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                    StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                    ClearColor = ClearColor(0, 0, 0, 1),
                }];
                rec.BeginRendering(new RenderingInfo
                {
                    RenderArea       = new VkRect2D { extent = new VkExtent2D { width = W, height = H } },
                    LayerCount       = 1,
                    ColorAttachments = color,
                });
                rec.SetViewport(new VkViewport { x = 0, y = 0, width = W, height = H, minDepth = 0, maxDepth = 1 });
                rec.SetScissor(new VkRect2D { extent = new VkExtent2D { width = W, height = H } });
                rec.BindPipeline(in pipeline);
                rec.BindIndexBuffer(in index, offset: 0, VkIndexType.VK_INDEX_TYPE_UINT16);
                rec.DrawIndexed(indexCount: 3, instanceCount: 2);
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

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        Assert.True(CountWhitePixels(readback.AsReadOnlySpan<byte>()) > 0,
            "DrawIndexed produced no white pixels — the indexed/instanced parameters did not reach vkCmdDrawIndexed.");
    }

    [Fact]
    public void DrawIndirect_GpuFilledIndirectBuffer_RendersTriangle()
    {
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");
        TestGate.RequireSpirv(VertSpvPath);
        TestGate.RequireSpirv(FragSpvPath);

        // Closes acceptance #2 of issue #45. The indirect buffer is
        // device-local — host has no mapping; the only way data lands in it
        // is the three FillBuffer calls below, which are vkCmdFillBuffer
        // (i.e. GPU-side writes). The buffer barrier between the fills and
        // the draw stops the driver from reordering the indirect read past
        // the writes.
        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        const uint W = 64, H = 64;
        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType   = VkImageType.VK_IMAGE_TYPE_2D,
                Format      = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width       = W, Height = H, Depth = 1,
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

        using var indirect = device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = (ulong)sizeof(VkDrawIndirectCommand),
                Usage = BufferUsage.IndirectBuffer | BufferUsage.TransferDst,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        const uint Bytes = W * H * 4;
        using var readback = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Bytes, Usage = BufferUsage.TransferDst },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                // VkDrawIndirectCommand layout: vertexCount@0, instanceCount@4,
                // firstVertex@8, firstInstance@12. FillBuffer writes a single
                // 32-bit pattern per call, so three calls cover the four words
                // (the trailing two zeros collapse into one 8-byte fill).
                rec.FillBuffer(in indirect, data: 3u, offset: 0, size: 4);
                rec.FillBuffer(in indirect, data: 1u, offset: 4, size: 4);
                rec.FillBuffer(in indirect, data: 0u, offset: 8, size: 8);

                BufferBarrier[] indirectBarrier =
                [
                    BufferBarrier.For(in indirect,
                        srcStage: Stage.AllTransfer,  srcAccess: Access.TransferWrite,
                        dstStage: Stage.DrawIndirect, dstAccess: Access.IndirectCommandRead),
                ];
                rec.PipelineBarrier(default, indirectBarrier, default);

                rec.PipelineBarrier(ImageBarrier.Transition(in image,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    srcStage: Stage.TopOfPipe,             srcAccess: Access.None,
                    dstStage: Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite));

                ReadOnlySpan<ColorAttachment> color = [new ColorAttachment
                {
                    View       = view,
                    Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                    StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                    ClearColor = ClearColor(0, 0, 0, 1),
                }];
                rec.BeginRendering(new RenderingInfo
                {
                    RenderArea       = new VkRect2D { extent = new VkExtent2D { width = W, height = H } },
                    LayerCount       = 1,
                    ColorAttachments = color,
                });
                rec.SetViewport(new VkViewport { x = 0, y = 0, width = W, height = H, minDepth = 0, maxDepth = 1 });
                rec.SetScissor(new VkRect2D { extent = new VkExtent2D { width = W, height = H } });
                rec.BindPipeline(in pipeline);
                rec.DrawIndirect(in indirect, offset: 0, drawCount: 1, stride: (uint)sizeof(VkDrawIndirectCommand));
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

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        Assert.True(CountWhitePixels(readback.AsReadOnlySpan<byte>()) > 0,
            "DrawIndirect produced no white pixels — the GPU-written indirect command did not reach vkCmdDrawIndirect.");
    }

    private static int CountWhitePixels(ReadOnlySpan<byte> rgba)
    {
        int hits = 0;
        for (int i = 0; i + 2 < rgba.Length; i += 4)
            if (rgba[i] == 255 && rgba[i + 1] == 255 && rgba[i + 2] == 255) hits++;
        return hits;
    }

    [Fact]
    public void BindVertexBuffers_RejectsMismatchedOffsets()
    {
        TestGate.RequireDriver();

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
        TestGate.RequireDriver();
        TestGate.RequireSpirv(VertSpvPath);
        TestGate.RequireSpirv(FragSpvPath);

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

        ReadOnlySpan<ColorAttachment> color = [new ColorAttachment
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
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();

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

    /// <summary>
    /// Issue 209: the behavioural payoff of the <c>readonly</c> recording
    /// surface. A stack-backed <see cref="ColorAttachment"/> span reaches
    /// <see cref="CommandRecorder.BeginRendering"/> without a heap array and
    /// without the caller declaring the recorder local <c>scoped</c>, so a
    /// render-pass open/close records zero bytes per frame. Record-only —
    /// nothing is submitted; the assertion is on the allocation counter, not
    /// on the GPU.
    /// </summary>
    /// <remarks>
    /// Run at <b>two</b> span lengths on purpose. Whether a collection
    /// expression lowers to an <c>InlineArray</c> stack local or to a heap
    /// array is a Roslyn lowering decision that can differ with element count,
    /// and neither <c>ScopedSpanProbe</c> nor a one-element case would notice a
    /// two-element span silently going to the heap — it would still compile
    /// and still pass validation.
    /// </remarks>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void BeginRendering_StackBackedColorAttachments_IsZeroAllocation(int attachmentCount)
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var image0 = CreateColorTarget(device, 64, 64);
        using var view0  = CreateColorView(device, in image0);
        using var image1 = CreateColorTarget(device, 64, 64);
        using var view1  = CreateColorView(device, in image1);
        using var pool   = new CommandBufferPool(device, family);

        void RecordOnce()
        {
            using (var rec = pool.Begin())
            {
                // The whole point: a collection-expression span, not a
                // ColorAttachment[]. Backed by an InlineArray local, so it is
                // reusable across iterations rather than growing the frame the
                // way a stackalloc in a loop body would. Built at length 2 and
                // sliced, so the two-element lowering is exercised even in the
                // one-attachment case.
                ReadOnlySpan<ColorAttachment> both =
                [
                    new ColorAttachment
                    {
                        View       = view0,
                        Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                        LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                        StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                        ClearColor = ClearColor(0, 0, 0, 1),
                    },
                    new ColorAttachment
                    {
                        View       = view1,
                        Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                        LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                        StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                        ClearColor = ClearColor(1, 0, 0, 1),
                    },
                ];
                ReadOnlySpan<ColorAttachment> color = both[..attachmentCount];

                rec.BeginRendering(new RenderingInfo
                {
                    RenderArea       = new VkRect2D { extent = new VkExtent2D { width = 64, height = 64 } },
                    LayerCount       = 1,
                    ColorAttachments = color,
                });
                rec.EndRendering();
                rec.End();
            }
            // Dispose (above, on scope exit) retires the buffer to _spent;
            // ResetForFrame then drains _spent into _idle. Reversing the order
            // leaks the buffer out of the pool's rotation.
            pool.ResetForFrame();
        }

        bool priorValidation = AhjoValidation.Enabled;
        AhjoValidation.Enabled = false;
        try
        {
            // Warm: JIT + tier-up on every path the measured loop touches.
            for (int i = 0; i < 32; i++) RecordOnce();

            // Two measured passes, the MeshPipeline_Build_IsZeroAllocation
            // shape: a tier-1 to tier-2 promotion can still fire on the first
            // measurement-sized loop and charge a one-shot allocation to this
            // thread. Only the second pass is asserted on.
            long before1 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) RecordOnce();
            _ = GC.GetAllocatedBytesForCurrentThread() - before1;

            long before2 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) RecordOnce();
            long after2 = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0, after2 - before2);
        }
        finally
        {
            AhjoValidation.Enabled = priorValidation;
        }
    }

    /// <summary>
    /// Issue 209: the multi-attachment shape under the validation layer. Two
    /// color attachments in one collection-expression span exercise
    /// <c>BeginRendering</c>'s <c>count &gt; 0</c> path into the eight-slot
    /// stack slab, against a pipeline declaring two color formats. Submitted,
    /// so the layer gets to judge the attachment/format match rather than the
    /// test merely not crashing.
    /// </summary>
    [Fact]
    public void BeginRendering_MultipleColorAttachments_StackBacked()
    {
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");
        TestGate.RequireValidationLayer();
        TestGate.RequireSpirv(VertSpvPath);
        TestGate.RequireSpirv(FragSpvPath);

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

        const uint W = 64, H = 64;
        using var image0 = CreateColorTarget(device, W, H);
        using var view0  = CreateColorView(device, in image0);
        using var image1 = CreateColorTarget(device, W, H);
        using var view1  = CreateColorView(device, in image1);

        using var vBlob = SpirvBlob.Load(VertSpvPath);
        using var fBlob = SpirvBlob.Load(FragSpvPath);
        using var vMod  = device.CreateShaderModule(vBlob.Words);
        using var fMod  = device.CreateShaderModule(fBlob.Words);
        using var layout = device.CreatePipelineLayout(default);

        ReadOnlySpan<VkFormat> colorFormats =
            [VkFormat.VK_FORMAT_R8G8B8A8_UNORM, VkFormat.VK_FORMAT_R8G8B8A8_UNORM];
        using var pipeline = device.BuildGraphicsPipeline()
            .WithStages(in vMod, in fMod)
            .WithDynamicRendering(colorFormats)
            .WithLayout(in layout)
            .Build();

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.PipelineBarrier(ImageBarrier.Transition(in image0,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    srcStage: Stage.TopOfPipe,             srcAccess: Access.None,
                    dstStage: Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite));
                rec.PipelineBarrier(ImageBarrier.Transition(in image1,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    srcStage: Stage.TopOfPipe,             srcAccess: Access.None,
                    dstStage: Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite));

                ReadOnlySpan<ColorAttachment> color =
                [
                    new ColorAttachment
                    {
                        View       = view0,
                        Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                        LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                        StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                        ClearColor = ClearColor(0, 0, 0, 1),
                    },
                    new ColorAttachment
                    {
                        View       = view1,
                        Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                        LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                        StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                        ClearColor = ClearColor(1, 0, 0, 1),
                    },
                ];

                rec.BeginRendering(new RenderingInfo
                {
                    RenderArea       = new VkRect2D { extent = new VkExtent2D { width = W, height = H } },
                    LayerCount       = 1,
                    ColorAttachments = color,
                });
                rec.SetViewport(new VkViewport { x = 0, y = 0, width = W, height = H, minDepth = 0, maxDepth = 1 });
                rec.SetScissor(new VkRect2D { extent = new VkExtent2D { width = W, height = H } });
                rec.BindPipeline(in pipeline);
                rec.Draw(vertexCount: 3);
                rec.EndRendering();

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        lock (errors)
            Assert.True(errors.Count == 0,
                "Validation errors recorded: " + string.Join("; ", errors.ConvertAll(e => e.Message)));
    }

    /// <summary>
    /// Issue 209: the <c>pDepth</c> branch under the new call shape. One color
    /// attachment as a collection-expression span plus a
    /// <see cref="DepthAttachment"/>, against a pipeline declaring the matching
    /// depth format. Submitted so the validation layer judges the depth
    /// layout/aspect/format triple.
    /// </summary>
    [Fact]
    public void BeginRendering_WithDepthAttachment_StackBacked()
    {
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");
        TestGate.RequireValidationLayer();
        TestGate.RequireSpirv(VertSpvPath);
        TestGate.RequireSpirv(FragSpvPath);

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

        const uint W = 64, H = 64;
        const VkFormat DepthFormat = VkFormat.VK_FORMAT_D32_SFLOAT;

        using var image = CreateColorTarget(device, W, H);
        using var view  = CreateColorView(device, in image);

        using var depthImage = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = DepthFormat,
                Width         = W, Height = H, Depth = 1,
                MipLevels     = 1, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.DepthStencilAttachment,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
        using var depthView = depthImage.CreateView(device, new ImageViewDescription
        {
            ViewType = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect   = VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT,
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
            .WithDepthStencil(testEnable: true, writeEnable: true,
                compareOp: VkCompareOp.VK_COMPARE_OP_LESS_OR_EQUAL)
            .WithDynamicRendering(colorFormats, depthFormat: DepthFormat)
            .WithLayout(in layout)
            .Build();

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
                rec.PipelineBarrier(ImageBarrier.Transition(in depthImage,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL,
                    srcStage: Stage.EarlyFragmentTests | Stage.LateFragmentTests, srcAccess: Access.None,
                    dstStage: Stage.EarlyFragmentTests | Stage.LateFragmentTests,
                    dstAccess: Access.DepthStencilAttachmentWrite,
                    aspect:   VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT));

                ReadOnlySpan<ColorAttachment> color = [new ColorAttachment
                {
                    View       = view,
                    Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                    StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                    ClearColor = ClearColor(0, 0, 0, 1),
                }];

                rec.BeginRendering(new RenderingInfo
                {
                    RenderArea       = new VkRect2D { extent = new VkExtent2D { width = W, height = H } },
                    LayerCount       = 1,
                    ColorAttachments = color,
                    DepthAttachment  = new DepthAttachment
                    {
                        View       = depthView,
                        Layout     = VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL,
                        LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                        StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_DONT_CARE,
                        ClearDepth = 1.0f,
                    },
                });
                rec.SetViewport(new VkViewport { x = 0, y = 0, width = W, height = H, minDepth = 0, maxDepth = 1 });
                rec.SetScissor(new VkRect2D { extent = new VkExtent2D { width = W, height = H } });
                rec.BindPipeline(in pipeline);
                rec.Draw(vertexCount: 3);
                rec.EndRendering();

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        lock (errors)
            Assert.True(errors.Count == 0,
                "Validation errors recorded: " + string.Join("; ", errors.ConvertAll(e => e.Message)));
    }

    private static Image CreateColorTarget(Device device, uint width, uint height) =>
        device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = width, Height = height, Depth = 1,
                MipLevels     = 1, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.ColorAttachment,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

    private static ImageView CreateColorView(Device device, in Image image) =>
        image.CreateView(device, new ImageViewDescription
        {
            ViewType = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect   = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            BaseMipLevel = 0, LevelCount = 1, BaseArrayLayer = 0, LayerCount = 1,
        });

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
