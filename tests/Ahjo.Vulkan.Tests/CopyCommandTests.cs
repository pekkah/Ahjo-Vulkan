using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers <see cref="CommandRecorder"/>'s copy / blit / clear / fill
/// surface (issue 18). Round-trip tests use a HOST → DEVICE → HOST
/// pattern through staging buffers; multi-region exercises a single
/// vkCmdCopyBuffer2 call with disjoint ranges.
/// </summary>
public sealed unsafe class CopyCommandTests
{
    [Fact]
    public void BufferCopyRegion_Default_Size_Maps_To_WholeSize()
    {
        var r = new BufferCopyRegion { SrcOffset = 0, DstOffset = 0, Size = 0 };
        var n = r.ToNative();
        Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_BUFFER_COPY_2, n.sType);
        Assert.Equal(~0ul, n.size);
    }

    [Fact]
    public void BufferCopyRegion_Of_Preserves_Offsets()
    {
        var r = BufferCopyRegion.Of(size: 1024, srcOffset: 64, dstOffset: 128);
        var n = r.ToNative();
        Assert.Equal(64ul,   n.srcOffset);
        Assert.Equal(128ul,  n.dstOffset);
        Assert.Equal(1024ul, n.size);
    }

    [Fact]
    public void BufferImageCopy_WholeImage_Defaults()
    {
        // Synthetic Image with no driver — we only inspect ToNative output.
        var copy = new BufferImageCopy
        {
            BufferOffset   = 0,
            Aspect         = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            LayerCount     = 1,
            ImageExtent    = new VkExtent3D { width = 64, height = 64, depth = 1 },
        };
        var n = copy.ToNative();
        Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_BUFFER_IMAGE_COPY_2, n.sType);
        Assert.Equal(0u,   n.bufferRowLength);
        Assert.Equal(0u,   n.bufferImageHeight);
        Assert.Equal(1u,   n.imageSubresource.layerCount);
        Assert.Equal(64u,  n.imageExtent.width);
    }

    [Fact]
    public void Buffer_To_Buffer_RoundTrip_1MB()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        const uint Size = 1024 * 1024; // 1 MiB

        using var src = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Size, Usage = BufferUsage.TransferSrc },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });
        using var dst = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Size, Usage = BufferUsage.TransferDst },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        // Fill source with a deterministic pattern.
        Span<byte> srcBytes = src.AsSpan<byte>();
        for (int i = 0; i < srcBytes.Length; i++) srcBytes[i] = (byte)(i * 31 + 7);

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.CopyBuffer(in src, in dst);
                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        ReadOnlySpan<byte> dstBytes = dst.AsReadOnlySpan<byte>();
        for (int i = 0; i < dstBytes.Length; i++)
            Assert.Equal((byte)(i * 31 + 7), dstBytes[i]);
    }

    [Fact]
    public void Buffer_To_Buffer_MultiRegion_DisjointCopies()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        const uint Count = 1024;        // uints
        const uint Bytes = Count * 4;

        using var src = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Bytes, Usage = BufferUsage.TransferSrc },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });
        using var dst = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Bytes, Usage = BufferUsage.TransferDst },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        Span<uint> s = src.AsSpan<uint>();
        for (int i = 0; i < s.Length; i++) s[i] = (uint)i;

        // dst already zeroed by the allocator; copy 4 disjoint 64-uint slabs:
        // src[0..64]   → dst[256..320]
        // src[64..128] → dst[0..64]
        // src[256..320]→ dst[768..832]
        // src[512..576]→ dst[512..576]
        ReadOnlySpan<BufferCopyRegion> regions =
        [
            BufferCopyRegion.Of(size: 64 * 4, srcOffset: 0   * 4, dstOffset: 256 * 4),
            BufferCopyRegion.Of(size: 64 * 4, srcOffset: 64  * 4, dstOffset: 0   * 4),
            BufferCopyRegion.Of(size: 64 * 4, srcOffset: 256 * 4, dstOffset: 768 * 4),
            BufferCopyRegion.Of(size: 64 * 4, srcOffset: 512 * 4, dstOffset: 512 * 4),
        ];

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.CopyBuffer(in src, in dst, regions);
                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        ReadOnlySpan<uint> d = dst.AsReadOnlySpan<uint>();

        // Each copied slab.
        for (int i = 0; i < 64; i++) Assert.Equal((uint)(0   + i), d[256 + i]);
        for (int i = 0; i < 64; i++) Assert.Equal((uint)(64  + i), d[0   + i]);
        for (int i = 0; i < 64; i++) Assert.Equal((uint)(256 + i), d[768 + i]);
        for (int i = 0; i < 64; i++) Assert.Equal((uint)(512 + i), d[512 + i]);

        // Holes between the slabs must still be zero.
        for (int i = 64;  i < 256; i++) Assert.Equal(0u, d[i]);
        for (int i = 320; i < 512; i++) Assert.Equal(0u, d[i]);
        for (int i = 576; i < 768; i++) Assert.Equal(0u, d[i]);
        for (int i = 832; i < 1024; i++) Assert.Equal(0u, d[i]);
    }

    [Fact]
    public void FillBuffer_Sets_Pattern()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        const uint Count = 256;
        using var buf = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Count * 4, Usage = BufferUsage.TransferDst },
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
                rec.FillBuffer(in buf, data: 0xDEADBEEFu);
                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        ReadOnlySpan<uint> data = buf.AsReadOnlySpan<uint>();
        for (int i = 0; i < (int)Count; i++) Assert.Equal(0xDEADBEEFu, data[i]);
    }

    [Fact]
    public void BufferImage_RoundTrip_Through_Image()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Mesa lavapipe SIGSEGVs inside the driver during the buffer↔image copy submission.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        const uint W = 64, H = 64;
        const uint TexBytes = W * H * 4;

        // Host-visible upload + readback buffers.
        using var upload = device.Allocator.CreateBuffer(
            new BufferDescription { Size = TexBytes, Usage = BufferUsage.TransferSrc },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });
        using var download = device.Allocator.CreateBuffer(
            new BufferDescription { Size = TexBytes, Usage = BufferUsage.TransferDst },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        // Write a recognisable gradient into the upload buffer.
        Span<byte> u = upload.AsSpan<byte>();
        for (int i = 0; i < u.Length; i++) u[i] = (byte)((i * 17) ^ 0x5A);

        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType   = VkImageType.VK_IMAGE_TYPE_2D,
                Format      = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width       = W, Height = H, Depth = 1,
                MipLevels   = 1, ArrayLayers = 1,
                Samples     = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling      = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage       = ImageUsage.TransferSrc | ImageUsage.TransferDst,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                // UNDEFINED → TRANSFER_DST → upload → TRANSFER_SRC → download.
                rec.PipelineBarrier(ImageBarrier.Transition(in image,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    srcStage: Stage.TopOfPipe,   srcAccess: Access.None,
                    dstStage: Stage.AllTransfer, dstAccess: Access.TransferWrite));

                rec.CopyBufferToImage(in upload, in image,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    BufferImageCopy.WholeImage(in image));

                rec.PipelineBarrier(ImageBarrier.Transition(in image,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    srcStage: Stage.AllTransfer, srcAccess: Access.TransferWrite,
                    dstStage: Stage.AllTransfer, dstAccess: Access.TransferRead));

                rec.CopyImageToBuffer(in image,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    in download,
                    BufferImageCopy.WholeImage(in image));

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(10)));
        }
        finally { fencePool.Release(fence); }

        ReadOnlySpan<byte> d = download.AsReadOnlySpan<byte>();
        for (int i = 0; i < d.Length; i++)
            Assert.Equal((byte)((i * 17) ^ 0x5A), d[i]);
    }

    [Fact]
    public void ClearColorImage_Whole_Image()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        const uint W = 16, H = 16;
        const uint TexBytes = W * H * 4;

        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType   = VkImageType.VK_IMAGE_TYPE_2D,
                Format      = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width       = W, Height = H, Depth = 1,
                MipLevels   = 1, ArrayLayers = 1,
                Samples     = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling      = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage       = ImageUsage.TransferSrc | ImageUsage.TransferDst,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using var download = device.Allocator.CreateBuffer(
            new BufferDescription { Size = TexBytes, Usage = BufferUsage.TransferDst },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        VkClearColorValue clear = default;
        clear.float32[0] = 0.25f;
        clear.float32[1] = 0.50f;
        clear.float32[2] = 0.75f;
        clear.float32[3] = 1.00f;

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
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    srcStage: Stage.TopOfPipe,   srcAccess: Access.None,
                    dstStage: Stage.AllTransfer, dstAccess: Access.TransferWrite));

                rec.ClearColorImage(in image,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, in clear);

                rec.PipelineBarrier(ImageBarrier.Transition(in image,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    srcStage: Stage.AllTransfer, srcAccess: Access.TransferWrite,
                    dstStage: Stage.AllTransfer, dstAccess: Access.TransferRead));

                rec.CopyImageToBuffer(in image,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    in download,
                    BufferImageCopy.WholeImage(in image));

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(10)));
        }
        finally { fencePool.Release(fence); }

        ReadOnlySpan<byte> d = download.AsReadOnlySpan<byte>();
        // 0.25 → 64, 0.5 → 128, 0.75 → 191, 1.0 → 255 (UNORM8 nearest).
        for (int p = 0; p < (int)(W * H); p++)
        {
            int o = p * 4;
            Assert.InRange(d[o + 0], (byte)63,  (byte)64);
            Assert.InRange(d[o + 1], (byte)127, (byte)128);
            Assert.InRange(d[o + 2], (byte)190, (byte)191);
            Assert.Equal((byte)255, d[o + 3]);
        }
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
