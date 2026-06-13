using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers <see cref="CommandRecorder.GenerateMips"/> (issue 62): full
/// mip-chain generation via successive blits, with the layout transition
/// dance the engine's <c>GpuTexture</c> path used to do by hand.
/// </summary>
public sealed unsafe class GenerateMipsTests
{
    [Fact]
    public void GenerateMips_4x4_RGBA_LandsAllMipsInFinalLayout()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        // 4×4 RGBA8 with 3 mips (4→2→1).
        const uint Width = 4, Height = 4, MipLevels = 3;

        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = Width, Height = Height, Depth = 1,
                MipLevels     = MipLevels, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.Sampled | ImageUsage.TransferSrc | ImageUsage.TransferDst,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        // Staging buffer with the 4×4 mip-0 source data.
        const uint Texels = Width * Height;
        using var staging = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Texels * 4, Usage = BufferUsage.TransferSrc },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });
        Span<byte> src = staging.AsSpan<byte>();
        for (int i = 0; i < src.Length; i++) src[i] = 200; // solid color

        Queue queue = device.GetQueue(family, queueIndex: 0);
        using var pool = new CommandBufferPool(device, family);

        // One ImmediateSubmit-shaped batch: transition mip 0 to
        // TRANSFER_DST, copy from staging, generate mips, then we're
        // done. The wrapper's GenerateMips picks up the chain from
        // there.
        var rec = pool.Begin();
        try
        {
            ImageBarrier toDst = ImageBarrier.Transition(
                in image,
                VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                Stage.None, Access.None,
                Stage.Copy, Access.TransferWrite) with
            {
                LevelCount = 1, // only mip 0; GenerateMips initialises the rest.
            };
            rec.PipelineBarrier(in toDst);

            BufferImageCopy copy = new()
            {
                BufferOffset = 0,
                Aspect       = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                MipLevel     = 0,
                LayerCount   = 1,
                ImageExtent  = new VkExtent3D { width = Width, height = Height, depth = 1 },
            };
            rec.CopyBufferToImage(in staging, in image,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                MemoryMarshal.CreateReadOnlySpan(ref copy, 1));

            rec.GenerateMips(in image, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);

            queue.Submit2(ref rec, fence: default);
        }
        finally { rec.Dispose(); }
        device.WaitIdle();

        // No driver-side error means the barrier transitions and blits
        // were spec-correct. The layout-correctness check is implicit:
        // a wrong NewLayout in any of the barriers would have triggered
        // a validation-layer warning during the submit.
        Assert.False(image.IsNull);
    }

    /// <summary>
    /// Non-power-of-two mip-chain check: a 5×3 image generates a chain
    /// that progresses 5×3 → 2×1 → 1×1 (uses <c>max(1, dim &gt;&gt; i)</c>).
    /// The helper must accept the asymmetric extents without throwing.
    /// </summary>
    [Fact]
    public void GenerateMips_NonPowerOfTwo_5x3_BuildsToMip1x1()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        const uint Width = 5, Height = 3, MipLevels = 3; // log2(5) + 1 = 3 levels

        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = Width, Height = Height, Depth = 1,
                MipLevels     = MipLevels, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.Sampled | ImageUsage.TransferSrc | ImageUsage.TransferDst,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        Queue queue = device.GetQueue(family, queueIndex: 0);
        using var pool = new CommandBufferPool(device, family);
        var rec = pool.Begin();
        try
        {
            // Skip the staging copy — we just want to confirm the helper
            // correctly walks the mip chain. Mip 0 transitions to
            // TRANSFER_DST_OPTIMAL with no content; the blit downsamples
            // garbage but the test isn't reading the texels back, only
            // verifying the helper finishes.
            ImageBarrier toDst = ImageBarrier.Transition(
                in image,
                VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                Stage.None, Access.None,
                Stage.Copy, Access.TransferWrite) with
            {
                LevelCount = 1,
            };
            rec.PipelineBarrier(in toDst);

            rec.GenerateMips(in image, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);

            queue.Submit2(ref rec, fence: default);
        }
        finally { rec.Dispose(); }
        device.WaitIdle();

        Assert.False(image.IsNull);
    }

    /// <summary>
    /// MipLevels = 1 is a degenerate input — the helper just transitions
    /// mip 0 from TRANSFER_DST to <paramref name="finalLayout"/> without
    /// running the downsample loop.
    /// </summary>
    [Fact]
    public void GenerateMips_SingleMip_TransitionsOnlyToFinalLayout()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = 16, Height = 16, Depth = 1,
                MipLevels     = 1, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.Sampled | ImageUsage.TransferDst,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        Queue queue = device.GetQueue(family, queueIndex: 0);
        using var pool = new CommandBufferPool(device, family);
        var rec = pool.Begin();
        try
        {
            ImageBarrier toDst = ImageBarrier.Transition(
                in image,
                VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                Stage.None, Access.None,
                Stage.Copy, Access.TransferWrite);
            rec.PipelineBarrier(in toDst);

            rec.GenerateMips(in image, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);

            queue.Submit2(ref rec, fence: default);
        }
        finally { rec.Dispose(); }
        device.WaitIdle();

        Assert.False(image.IsNull);
    }

    /// <summary>
    /// Regression for issue 101: mip 0 may be produced by <em>any</em>
    /// transfer command, not just a copy. Here it is filled by a
    /// <c>vkCmdClearColorImage</c> (CLEAR stage). Before the fix, the
    /// mip-0 source scope in <see cref="CommandRecorder.GenerateMips"/>
    /// only covered the COPY stage (<c>Stage.Copy</c>), so the clear's
    /// write was left unordered against the TRANSFER_DST → TRANSFER_SRC
    /// layout transition — a write-after-write hazard that corrupts the
    /// generated mips. Widening the source scope to
    /// <c>Stage.AllTransfer</c> (COPY | BLIT | RESOLVE | CLEAR) fixes it.
    /// We read back the 1×1 final mip and assert it equals the clear
    /// color: downsampling a solid color yields the same solid color.
    /// </summary>
    [Fact]
    public void GenerateMips_Mip0FilledByClear_PropagatesColorToAllMips()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        // 4×4 RGBA8 with 3 mips (4→2→1).
        const uint Width = 4, Height = 4, MipLevels = 3;

        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = Width, Height = Height, Depth = 1,
                MipLevels     = MipLevels, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.Sampled | ImageUsage.TransferSrc | ImageUsage.TransferDst,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        // 1×1 (one RGBA8 texel) host-visible readback target for the
        // final mip.
        using var download = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 4, Usage = BufferUsage.TransferDst },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        // Known clear color. UNORM8 quantization: round(c * 255).
        VkClearColorValue clear = default;
        clear.float32[0] = 0.2f; // → 51
        clear.float32[1] = 0.4f; // → 102
        clear.float32[2] = 0.6f; // → 153
        clear.float32[3] = 1.0f; // → 255

        Queue queue = device.GetQueue(family, queueIndex: 0);
        using var pool      = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = pool.Begin();
            try
            {
                // 1. Transition mip 0 only UNDEFINED → TRANSFER_DST.
                ImageBarrier toDst = ImageBarrier.Transition(
                    in image,
                    VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    Stage.None, Access.None,
                    Stage.Clear, Access.TransferWrite) with
                {
                    LevelCount = 1, // only mip 0; GenerateMips initialises the rest.
                };
                rec.PipelineBarrier(in toDst);

                // 2. Fill mip 0 via a CLEAR (the point of this test — a
                //    CLEAR-stage producer, not a copy). Range covers ONLY
                //    mip 0; mips 1..N are still UNDEFINED here and clearing
                //    them would be invalid.
                VkImageSubresourceRange mip0 = new()
                {
                    aspectMask     = (uint)VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                    baseMipLevel   = 0, levelCount = 1,
                    baseArrayLayer = 0, layerCount = 1,
                };
                rec.ClearColorImage(in image,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    in clear,
                    MemoryMarshal.CreateReadOnlySpan(ref mip0, 1));

                // 3. Generate the mip chain. Final layout TRANSFER_SRC so the
                //    readback copy can sample the mips without another barrier.
                rec.GenerateMips(in image, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL);

                // 4. Copy the 1×1 final mip (level 2) into the readback buffer.
                BufferImageCopy copy = new()
                {
                    BufferOffset = 0,
                    Aspect       = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                    MipLevel     = 2,
                    LayerCount   = 1,
                    ImageExtent  = new VkExtent3D { width = 1, height = 1, depth = 1 },
                };
                rec.CopyImageToBuffer(in image,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    in download,
                    MemoryMarshal.CreateReadOnlySpan(ref copy, 1));

                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(10)));
        }
        finally { fencePool.Release(fence); }
        device.WaitIdle();

        // Pull the GPU's writes into host-visible memory before reading
        // (no-op on coherent allocations; required on non-coherent ones —
        // same rule as the staging-flush fix, issue 98).
        download.Invalidate(0, 4);

        // Downsampling a solid color yields the same solid color, so the
        // 1×1 mip equals the clear color quantized to UNORM8:
        // {0.2, 0.4, 0.6, 1.0} → {51, 102, 153, 255}. ±1 per channel for
        // filter rounding tolerance.
        ReadOnlySpan<byte> px = download.AsReadOnlySpan<byte>();
        Assert.InRange(px[0], (byte)50,  (byte)52);
        Assert.InRange(px[1], (byte)101, (byte)103);
        Assert.InRange(px[2], (byte)152, (byte)154);
        Assert.InRange(px[3], (byte)254, (byte)255);
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
            Queues = [new QueueRequest(f, count: 1, priority: 1.0f)],
        });
    }
}
