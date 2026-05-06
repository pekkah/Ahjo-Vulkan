using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers the typed sync2 barrier wrappers: factory defaults, ToNative
/// field mapping, and a host-side multi-barrier batch through
/// <see cref="CommandRecorder.PipelineBarrier"/>.
/// </summary>
public sealed unsafe class PipelineBarrierTests
{
    // ---- Pure host-side checks (no driver needed) ----

    [Fact]
    public void MemoryBarrier_Between_Maps_Sync2_Fields()
    {
        var b = MemoryBarrier.Between(
            Stage.ComputeShader,         Access.ShaderStorageWrite,
            Stage.FragmentShader,        Access.ShaderSampledRead);

        var n = b.ToNative();
        Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_MEMORY_BARRIER_2, n.sType);
        Assert.Equal((ulong)Stage.ComputeShader,        n.srcStageMask);
        Assert.Equal((ulong)Access.ShaderStorageWrite,  n.srcAccessMask);
        Assert.Equal((ulong)Stage.FragmentShader,       n.dstStageMask);
        Assert.Equal((ulong)Access.ShaderSampledRead,   n.dstAccessMask);
    }

    [Fact]
    public void BufferBarrier_For_Defaults_Size_To_WholeSize()
    {
        var b = new BufferBarrier
        {
            Buffer    = (nint)0x1234,
            SrcStage  = Stage.Copy,         SrcAccess = Access.TransferWrite,
            DstStage  = Stage.VertexShader, DstAccess = Access.UniformRead,
            // Offset = 0, Size = 0 (default) → ToNative remaps to VK_WHOLE_SIZE.
        };

        var n = b.ToNative();
        Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_BUFFER_MEMORY_BARRIER_2, n.sType);
        Assert.Equal(~0ul, n.size);
        Assert.Equal(0ul,  n.offset);
        Assert.Equal(~0u,  n.srcQueueFamilyIndex);
        Assert.Equal(~0u,  n.dstQueueFamilyIndex);
    }

    [Fact]
    public void BufferBarrier_For_Preserves_NonZero_Size()
    {
        var b = new BufferBarrier
        {
            Buffer    = (nint)0x1,
            SrcStage  = Stage.Copy,         SrcAccess = Access.TransferWrite,
            DstStage  = Stage.VertexShader, DstAccess = Access.IndexRead,
            Offset    = 64,
            Size      = 1024,
        };

        var n = b.ToNative();
        Assert.Equal(64ul,   n.offset);
        Assert.Equal(1024ul, n.size);
    }

    [Fact]
    public void ImageBarrier_Default_Has_Null_Image_Handle()
    {
        ImageBarrier b = default;
        Assert.Equal(IntPtr.Zero, b.Image);
    }

    [Fact]
    public void ImageBarrier_ToNative_Defaults_Counts_To_One()
    {
        // Bypass the Transition(...) factory to exercise the "user
        // forgot to set LevelCount/LayerCount" guard in ToNative.
        var b = new ImageBarrier
        {
            Image     = (nint)0x42,
            SrcStage  = Stage.TopOfPipe,
            DstStage  = Stage.ColorAttachmentOutput,
            DstAccess = Access.ColorAttachmentWrite,
            OldLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            NewLayout = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
            Aspect    = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
        };
        var n = b.ToNative();
        Assert.Equal(1u, n.subresourceRange.levelCount);
        Assert.Equal(1u, n.subresourceRange.layerCount);
        Assert.Equal((uint)VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT, n.subresourceRange.aspectMask);
        Assert.Equal(~0u, n.srcQueueFamilyIndex);
        Assert.Equal(~0u, n.dstQueueFamilyIndex);
    }

    // ---- Driver-bound: actually issue the barrier ----

    [Fact]
    public void Image_Layout_Transitions_Through_Three_Layouts()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

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
                Usage       = ImageUsage.ColorAttachment | ImageUsage.TransferSrc | ImageUsage.TransferDst,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using var cmdPool = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.PipelineBarrier(ImageBarrier.Transition(
                    in image,
                    from:      VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    to:        VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    srcStage:  Stage.TopOfPipe,  srcAccess: Access.None,
                    dstStage:  Stage.AllTransfer, dstAccess: Access.TransferWrite));

                rec.PipelineBarrier(ImageBarrier.Transition(
                    in image,
                    from:      VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    to:        VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    srcStage:  Stage.AllTransfer,           srcAccess: Access.TransferWrite,
                    dstStage:  Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite));

                rec.PipelineBarrier(ImageBarrier.Transition(
                    in image,
                    from:      VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    to:        VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    srcStage:  Stage.ColorAttachmentOutput, srcAccess: Access.ColorAttachmentWrite,
                    dstStage:  Stage.AllTransfer,           dstAccess: Access.TransferRead));

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }
    }

    [Fact]
    public void Batched_Barriers_Memory_Buffer_Image_In_Single_Call()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType   = VkImageType.VK_IMAGE_TYPE_2D,
                Format      = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width       = 32, Height = 32, Depth = 1,
                MipLevels   = 1, ArrayLayers = 1,
                Samples     = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling      = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage       = ImageUsage.Sampled | ImageUsage.TransferDst,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using var buf = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 4096, Usage = BufferUsage.StorageBuffer | BufferUsage.TransferDst },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        var memBars = new[]
        {
            MemoryBarrier.Between(Stage.ComputeShader, Access.ShaderStorageWrite,
                                  Stage.FragmentShader, Access.ShaderSampledRead),
        };
        var bufBars = new[]
        {
            BufferBarrier.For(in buf,
                Stage.AllTransfer, Access.TransferWrite,
                Stage.VertexShader, Access.UniformRead),
        };
        var imgBars = new[]
        {
            ImageBarrier.Transition(in image,
                from:     VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                to:       VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                srcStage: Stage.TopOfPipe,    srcAccess: Access.None,
                dstStage: Stage.FragmentShader, dstAccess: Access.ShaderSampledRead),
        };

        using var cmdPool = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.PipelineBarrier(memBars, bufBars, imgBars);
                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }
    }

    [Fact]
    public void Empty_Barrier_Call_Is_NoOp()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);
        using var pool     = new CommandBufferPool(device, family);

        using var rec = pool.Begin();
        rec.PipelineBarrier(default, default, default); // empty mix → must not call into the driver
        rec.End();
    }

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
