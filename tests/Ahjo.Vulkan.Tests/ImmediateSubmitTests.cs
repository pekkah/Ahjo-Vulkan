using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers <see cref="Queue.WaitIdle"/> and <see cref="Queue.ImmediateSubmit"/>
/// (issue 61): the engine's universal one-shot record/submit/wait helper for
/// asset uploads, mip generation, IBL convolution, and other off-frame work.
/// </summary>
public sealed unsafe class ImmediateSubmitTests
{
    [Fact]
    public void Queue_WaitIdle_ReturnsSuccessOnIdleQueue()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        Queue queue = device.GetQueue(family, queueIndex: 0);
        queue.WaitIdle();
    }

    /// <summary>
    /// Canonical asset-upload pattern: stage 64 KiB host-side, copy to a
    /// device buffer through ImmediateSubmit, read back via a second
    /// staging buffer. ImmediateSubmit's WaitIdle gives the read a
    /// synchronous "ready" signal — no fence needed at the call site.
    /// </summary>
    [Fact]
    public void ImmediateSubmit_HostToDeviceCopy_ContentMatches()
    {
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        const uint Size = 64 * 1024; // 64 KiB

        using var staging = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Size, Usage = BufferUsage.TransferSrc },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });
        using var device_ = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Size, Usage = BufferUsage.TransferSrc | BufferUsage.TransferDst },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
        using var readback = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Size, Usage = BufferUsage.TransferDst },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        Span<byte> stagingBytes = staging.AsSpan<byte>();
        for (int i = 0; i < stagingBytes.Length; i++)
            stagingBytes[i] = (byte)(i * 13 + 5);

        Queue queue = device.GetQueue(family, queueIndex: 0);
        using var pool = new CommandBufferPool(device, family);

        // Two ImmediateSubmits: staging→device, then device→readback.
        // Engines split the steps when intermediate device-side processing
        // is needed; for this smoke test the second submit just exercises
        // the second pool reuse path.
        queue.ImmediateSubmit(pool, (ref CommandRecorder rec) =>
        {
            rec.CopyBuffer(in staging, in device_);
        });
        queue.ImmediateSubmit(pool, (ref CommandRecorder rec) =>
        {
            rec.CopyBuffer(in device_, in readback);
        });

        Span<byte> readbackBytes = readback.AsSpan<byte>();
        for (int i = 0; i < readbackBytes.Length; i++)
            Assert.Equal((byte)(i * 13 + 5), readbackBytes[i]);
    }

    /// <summary>
    /// Pool-reuse contract: after the first submit warms the pool, every
    /// subsequent ImmediateSubmit re-uses the same command buffer rather
    /// than allocating a new one. <see cref="CommandBufferPool.AllocatedCount"/>
    /// stays at 1.
    /// </summary>
    [Fact]
    public void ImmediateSubmit_RepeatedCalls_ReuseSameCommandBuffer()
    {
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        Queue queue = device.GetQueue(family, queueIndex: 0);
        using var pool = new CommandBufferPool(device, family);

        // After three calls the pool should still have allocated only one
        // buffer — every Retire pushes onto _spent, but ImmediateSubmit's
        // recorder lifetime spans only one call so the pool can re-issue
        // the same buffer (after a vkResetCommandPool inside ResetForFrame
        // — which we trigger between calls).
        for (int i = 0; i < 3; i++)
        {
            queue.ImmediateSubmit(pool, (ref CommandRecorder _) => { });
            // ImmediateSubmit doesn't reset the pool — it leaves the buffer
            // in _spent. ResetForFrame moves it back to _idle so the next
            // call reuses it; without the reset, the next Begin would
            // allocate a fresh buffer.
            pool.ResetForFrame();
        }

        Assert.Equal(1, pool.AllocatedCount);
    }

    /// <summary>
    /// Recording-time exception must not strand the command buffer in the
    /// pool's outstanding-set. The recorder's Dispose runs in finally so
    /// the next ResetForFrame doesn't trip its outstanding assert.
    /// </summary>
    [Fact]
    public void ImmediateSubmit_RecorderThrows_BufferStillRetired()
    {
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        Queue queue = device.GetQueue(family, queueIndex: 0);
        using var pool = new CommandBufferPool(device, family);

        Assert.Throws<InvalidOperationException>(() =>
            queue.ImmediateSubmit(pool, (ref CommandRecorder _) =>
                throw new InvalidOperationException("record-time fail")));

        // Outstanding must be back to zero — the throw shouldn't have left
        // the recorder un-retired.
        Assert.Equal(0, pool.OutstandingCount);
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
