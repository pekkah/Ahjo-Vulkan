using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers <see cref="StagingUploader"/> (issue 34): bump-allocation,
/// chunk growth, reset semantics, and a real GPU round-trip via the
/// recorder's CopyBuffer path.
/// </summary>
public sealed unsafe class StagingUploaderTests
{
    [Fact]
    public void Upload_4KiB_Floats_RoundTrips_Through_DeviceLocal()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        const int Count = 1024;          // 4 KiB of float
        const uint Bytes = Count * 4;
        float[] payload = new float[Count];
        for (int i = 0; i < Count; i++) payload[i] = i * 0.5f + 1.25f;

        using var staging  = new StagingUploader(device.Allocator);
        StagedUpload upload = staging.Upload<float>(payload);
        Assert.Equal(Bytes, upload.Size);
        Assert.False(upload.Source.IsNull);

        using var deviceLocal = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Bytes, Usage = BufferUsage.TransferSrc | BufferUsage.TransferDst },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
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
                Buffer staged = upload.Source;
                rec.CopyBuffer(in staged, in deviceLocal, upload.ToCopyRegion());
                var memBars = new[]
                {
                    MemoryBarrier.Between(Stage.AllTransfer, Access.TransferWrite,
                                          Stage.AllTransfer, Access.TransferRead),
                };
                rec.PipelineBarrier(memBars, default, default);
                rec.CopyBuffer(in deviceLocal, in readback, BufferCopyRegion.Of(size: Bytes));

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        ReadOnlySpan<float> got = readback.AsReadOnlySpan<float>();
        for (int i = 0; i < Count; i++)
            Assert.Equal(payload[i], got[i]);
    }

    [Fact]
    public void Reset_Rewinds_Heads_Without_Reallocating_Chunks()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        // Small chunk so consecutive uploads exhaust it on demand.
        using var staging = new StagingUploader(device.Allocator,
            chunkSize: 4096, alignment: StagingUploader.DefaultAlignment);

        // Frame 1: two 1 KiB uploads. Both fit in chunk 0.
        var u1 = staging.Upload<byte>(new byte[1024]);
        var u2 = staging.Upload<byte>(new byte[1024]);
        Assert.Equal(1, staging.ChunkCount);
        Assert.Equal(0ul,    u1.Offset);
        Assert.Equal(1024ul, u2.Offset);
        Assert.Equal(2048ul, staging.UsedBytes);

        // Frame 2: reset → both heads back to 0, chunks retained.
        staging.Reset();
        Assert.Equal(1, staging.ChunkCount);
        Assert.Equal(0ul, staging.UsedBytes);

        var u3 = staging.Upload<byte>(new byte[1024]);
        Assert.Equal(1, staging.ChunkCount); // no new chunk allocated
        Assert.Equal(0ul, u3.Offset);        // bumped from a clean head
    }

    [Fact]
    public void Grows_New_Chunk_When_Active_Is_Full()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        using var staging = new StagingUploader(device.Allocator, chunkSize: 4096);

        var u1 = staging.Upload<byte>(new byte[3072]); // chunk 0, head 3072
        var u2 = staging.Upload<byte>(new byte[2048]); // 3072 + 2048 = 5120 > 4096 → chunk 1
        Assert.Equal(2, staging.ChunkCount);

        // The second upload landed at offset 0 of the new chunk and the
        // returned Source must differ from the first upload's source.
        Assert.Equal(0ul, u2.Offset);
        Assert.True(u1.Source.Handle != u2.Source.Handle);
    }

    [Fact]
    public void Oversize_Upload_Allocates_OneOff_Chunk()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        using var staging = new StagingUploader(device.Allocator, chunkSize: 1024);

        var huge = new byte[8192]; // 8x the chunk size
        var up   = staging.Upload<byte>(huge);

        Assert.Equal(8192ul, up.Size);
        Assert.Equal(0ul,    up.Offset);
        Assert.Equal(1,      staging.ChunkCount);
        Assert.True(up.Source.Size >= 8192);
    }

    [Fact]
    public void Empty_Upload_Returns_Empty_StagedUpload()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        using var staging = new StagingUploader(device.Allocator);

        var up = staging.Upload<float>(ReadOnlySpan<float>.Empty);
        Assert.True(up.IsEmpty);
        Assert.Equal(0, staging.ChunkCount); // didn't even allocate a chunk
    }

    [Fact]
    public void Default_Constructor_Throws_On_Null_Allocator()
    {
        Assert.Throws<ArgumentException>(() => new StagingUploader(default));
    }

    [Fact]
    public void Constructor_Rejects_Non_PowerOfTwo_Alignment()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        Assert.Throws<ArgumentException>(() => new StagingUploader(device.Allocator, alignment: 12));
    }

    [Fact]
    public void StagedUpload_ToCopyRegion_Picks_Up_SrcOffset()
    {
        var up   = new StagedUpload(default, Offset: 256, Size: 1024);
        var rgn  = up.ToCopyRegion(dstOffset: 64);
        Assert.Equal(256ul,  rgn.SrcOffset);
        Assert.Equal(64ul,   rgn.DstOffset);
        Assert.Equal(1024ul, rgn.Size);
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
