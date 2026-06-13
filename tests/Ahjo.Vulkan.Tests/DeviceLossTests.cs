using System.Diagnostics;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Issue #120: <see cref="Device.IsLost"/> is the single post-loss policy
/// switch — waits return immediately, status queries throw
/// deterministically, pools release without querying, teardown completes.
/// Real device loss can't be provoked portably, so these tests drive the
/// flag through the internal <see cref="Device.MarkLost"/> seam
/// (<c>InternalsVisibleTo</c>); the choke-point test exercises the same
/// path a real <c>VK_ERROR_DEVICE_LOST</c> takes through
/// <see cref="ResultExtensions"/>.
/// </summary>
public sealed class DeviceLossTests
{
    [Fact]
    public void IsLost_DefaultsFalse_MarkLostFlipsOnce()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        Assert.False(device.IsLost);
        device.MarkLost();
        Assert.True(device.IsLost);
        device.MarkLost(); // idempotent
        Assert.True(device.IsLost);
    }

    /// <summary>
    /// The hang killer: an infinite wait on a never-signaled fence must
    /// return immediately once the device is marked lost, without calling
    /// the driver. Pre-#120 this was the recovery path most likely to
    /// freeze teardown.
    /// </summary>
    [Fact]
    public void Fence_Wait_AfterLoss_ReturnsDeviceLostImmediately()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        using var pool     = new FencePool(device);

        var fence = pool.Acquire(initiallySignaled: false);
        device.MarkLost();

        var sw = Stopwatch.StartNew();
        WaitState state = fence.Wait(Timeout.InfiniteTimeSpan);
        sw.Stop();

        Assert.Equal(WaitState.DeviceLost, state);
        // Generous bound — the point is "did not block on the fence";
        // the fast path is a volatile read in front of the syscall.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"Wait took {sw.Elapsed} — the IsLost fast path did not engage.");

        pool.Release(fence);
    }

    [Fact]
    public void Fence_IsSignaled_AfterLoss_ThrowsDeviceLost_Deterministically()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        using var pool     = new FencePool(device);

        var fence = pool.Acquire(initiallySignaled: true);
        device.MarkLost();

        var ex = Assert.Throws<VulkanException>(() => fence.IsSignaled);
        Assert.Equal(VkResult.VK_ERROR_DEVICE_LOST, ex.Result);

        pool.Release(fence);
    }

    /// <summary>
    /// #107's shape through the plain overload: Release evaluates
    /// IsSignaled, which throws after loss — the pool must skip the
    /// status query when the device is lost so dispose loops survive.
    /// </summary>
    [Fact]
    public void FencePool_Release_AfterLoss_SkipsStatusQuery()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        using var pool     = new FencePool(device);

        var fence = pool.Acquire(initiallySignaled: true);
        device.MarkLost();

        pool.Release(fence); // must not throw
        Assert.Equal(1, pool.IdleCount);
    }

    [Fact]
    public void TimelineSemaphore_WaitFor_AfterLoss_ReturnsDeviceLostImmediately()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        using var pool     = new SemaphorePool(device);

        var timeline = pool.AcquireTimeline();
        device.MarkLost();

        // Value 100 will never be signaled; only the fast path returns.
        Assert.Equal(WaitState.DeviceLost, timeline.WaitFor(100, Timeout.InfiniteTimeSpan));

        pool.Release(timeline);
    }

    /// <summary>
    /// The full #107 scenario: a ring with a pending submit is torn down
    /// after device loss. Pre-fix, the in-flight fence's status query
    /// threw out of Slot.Dispose and stranded the remaining slots'
    /// pools. The GPU work is drained (WaitIdle) before the loss is
    /// faked so the teardown is exercising the bookkeeping, not racing
    /// real submissions.
    /// </summary>
    [Fact]
    public void FrameRing_Dispose_AfterLoss_WithPendingSubmit_CompletesAllSlots()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        var ring  = new FrameRing(device, framesInFlight: 2, queueFamily: family);
        var queue = device.GetQueue(family, 0);
        int[] payload = new int[64];

        using (var frame = ring.BeginFrame())
        {
            var rec = frame.CommandBuffers.Begin();
            try
            {
                StagedUpload up = frame.Staging.Upload<int>(payload);
                Buffer staged = up.Source;
                rec.FillBuffer(in staged, 0xCAFEBABE, offset: up.Offset, size: up.Size);
                frame.Submit(queue, ref rec);
            }
            finally { rec.Dispose(); }
        }

        device.WaitIdle();
        device.MarkLost();

        var captured = new List<string>();
        DiagnosticSink original = AhjoDiagnostics.Sink;
        try
        {
            AhjoDiagnostics.Sink = (_, source, message) => { if (source == "FrameRing") captured.Add(message); };
            ring.Dispose(); // must complete every slot without throwing
        }
        finally
        {
            AhjoDiagnostics.Sink = original;
        }

        // The pending-submit slot's wait fast-returned DeviceLost and the
        // teardown logged it through the sink instead of stderr.
        Assert.Contains(captured, m => m.Contains("teardown proceeds"));
    }

    /// <summary>
    /// The #123 choke point feeds the flag: any wrapper call failing with
    /// VK_ERROR_DEVICE_LOST through ThrowIfFailed marks every live device.
    /// </summary>
    [Fact]
    public void ResultExtensions_DeviceLostThrow_MarksLiveDevices()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        Assert.False(device.IsLost);

        var ex = Assert.Throws<VulkanException>(() => VkResult.VK_ERROR_DEVICE_LOST.ThrowIfFailed());
        Assert.Equal(VkResult.VK_ERROR_DEVICE_LOST, ex.Result);
        Assert.True(device.IsLost);
    }

    [Fact]
    public void ResultExtensions_DeviceLostThrow_DoesNotMarkDisposedDevices()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        var device = CreateGraphicsDevice(instance, out _);
        device.Dispose(); // unregisters from the live registry

        Assert.Throws<VulkanException>(() => VkResult.VK_ERROR_DEVICE_LOST.ThrowIfFailed());
        Assert.False(device.IsLost);
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
