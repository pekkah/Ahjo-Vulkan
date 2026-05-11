using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers <see cref="StagingBatch"/> (issue 63): N pending uploads
/// flushed in a single command buffer with one wait-idle. The engine's
/// scene-init path enqueues hundreds of mesh / texture uploads through
/// this shape and amortizes the wait-idle cost across one wait, not one
/// per upload.
/// </summary>
public sealed unsafe class StagingBatchTests
{
    [Fact]
    public void Flush_SixteenUploads_AllDestinationsContainExpectedBytes()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        const int Count    = 16;
        const int PerSize  = 8 * 1024; // 8 KiB
        Buffer[] dests     = new Buffer[Count];
        Buffer[] readbacks = new Buffer[Count];
        try
        {
            for (int i = 0; i < Count; i++)
            {
                dests[i] = device.Allocator.CreateBuffer(
                    new BufferDescription { Size = PerSize, Usage = BufferUsage.TransferSrc | BufferUsage.TransferDst },
                    new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
                readbacks[i] = device.Allocator.CreateBuffer(
                    new BufferDescription { Size = PerSize, Usage = BufferUsage.TransferDst },
                    new AllocationDescription
                    {
                        Usage = MemoryUsage.AutoPreferHost,
                        Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
                    });
            }

            using var batch = new StagingBatch(device.Allocator);
            using var pool  = new CommandBufferPool(device, family);
            Queue queue = device.GetQueue(family, queueIndex: 0);

            // Each upload writes a deterministic byte pattern keyed on i.
            for (int i = 0; i < Count; i++)
            {
                byte[] payload = new byte[PerSize];
                for (int j = 0; j < PerSize; j++) payload[j] = (byte)((i * 17 + j) & 0xFF);
                batch.EnqueueUpload<byte>(payload, in dests[i]);
            }
            Assert.Equal(Count, batch.PendingCount);

            batch.Flush(queue, pool);

            // PendingCount drops back to zero — Flush resets the batch.
            Assert.Equal(0, batch.PendingCount);

            // Verify by copying each destination → its readback buffer in
            // a single ImmediateSubmit-shaped command buffer.
            var rec = pool.Begin();
            try
            {
                for (int i = 0; i < Count; i++)
                    rec.CopyBuffer(in dests[i], in readbacks[i]);
                queue.Submit2(ref rec, fence: default);
            }
            finally { rec.Dispose(); }
            device.WaitIdle();

            for (int i = 0; i < Count; i++)
            {
                Span<byte> got = readbacks[i].AsSpan<byte>();
                for (int j = 0; j < PerSize; j++)
                    Assert.Equal((byte)((i * 17 + j) & 0xFF), got[j]);
            }
        }
        finally
        {
            for (int i = 0; i < Count; i++) { dests[i].Dispose(); readbacks[i].Dispose(); }
        }
    }

    /// <summary>
    /// Two flushes recycle the same chunk: chunk count after the second
    /// flush stays at the post-first-flush count, with no per-flush
    /// VMA growth.
    /// </summary>
    [Fact]
    public void Flush_RepeatedRounds_RecycleChunks()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var dst = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 4096, Usage = BufferUsage.TransferDst },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using var batch = new StagingBatch(device.Allocator);
        using var pool  = new CommandBufferPool(device, family);
        Queue queue = device.GetQueue(family, queueIndex: 0);

        byte[] payload = new byte[1024];

        // First flush — at least one chunk allocated.
        batch.EnqueueUpload<byte>(payload, in dst);
        batch.Flush(queue, pool);
        int chunksAfterFirst = batch.ChunkCount;
        Assert.True(chunksAfterFirst >= 1);

        // Second flush — uses the same chunks (heads reset on flush).
        batch.EnqueueUpload<byte>(payload, in dst);
        batch.Flush(queue, pool);
        Assert.Equal(chunksAfterFirst, batch.ChunkCount);
    }

    /// <summary>
    /// Single oversize upload (> default chunk size) still works — the
    /// growth path allocates a fresh chunk sized to fit the payload.
    /// </summary>
    [Fact]
    public void EnqueueUpload_OversizePayload_GrowsChunk()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint _);

        using var dst = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 8 * 1024 * 1024, Usage = BufferUsage.TransferDst },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        // Smaller default chunk so a 6 MiB upload exceeds it.
        using var batch = new StagingBatch(device.Allocator, chunkSize: 4UL * 1024 * 1024);

        byte[] big = new byte[6 * 1024 * 1024];
        batch.EnqueueUpload<byte>(big, in dst);
        Assert.Equal(1, batch.PendingCount);
        Assert.True(batch.ChunkCount >= 1);
    }

    [Fact]
    public void Flush_EmptyBatch_NoOp()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software ICD (SwiftShader): the device + instance dispose path crashes inside " +
            "libvk_swiftshader on Linux even though the Flush call itself is an early-return " +
            "no-op. Known SwiftShader sensitivity to cumulative vkDestroyDevice paths; real " +
            "drivers (NVIDIA / AMD / Intel) exercise this fixture on the Windows CI leg.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var batch = new StagingBatch(device.Allocator);
        using var pool  = new CommandBufferPool(device, family);
        Queue queue = device.GetQueue(family, queueIndex: 0);

        batch.Flush(queue, pool); // no throw, no submit
        Assert.Equal(0, batch.PendingCount);
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
