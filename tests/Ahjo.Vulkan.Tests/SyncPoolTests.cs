using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class SyncPoolTests
{
    [Fact]
    public void FencePool_Acquire_InitiallySignaled_IsSignaledTrue()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var pool     = new FencePool(device);

        var fence = pool.Acquire(initiallySignaled: true);
        try
        {
            Assert.True(fence.IsSignaled);
            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.Zero));
        }
        finally { pool.Release(fence); }
    }

    [Fact]
    public void FencePool_Unsignaled_Wait_ReturnsTimeout()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var pool     = new FencePool(device);

        var fence = pool.Acquire(initiallySignaled: false);
        try
        {
            Assert.False(fence.IsSignaled);
            Assert.Equal(WaitState.Timeout, fence.Wait(TimeSpan.FromMilliseconds(1)));
        }
        finally { pool.Release(fence); }
    }

    [Fact]
    public void FencePool_AcquireRelease_RecyclesHandle()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var pool     = new FencePool(device);

        var first = pool.Acquire();
        pool.Release(first);
        var second = pool.Acquire();

        unsafe { Assert.True(first.Handle == second.Handle); }
        Assert.Equal(1, pool.AllocatedCount);
    }

    /// <summary>
    /// Regression: <c>Acquire(initiallySignaled: true)</c> used to ignore
    /// the parameter on the recycle path and return whatever state the
    /// previous user left, so a caller asking for a signaled fence could
    /// silently get an unsignaled one and then deadlock on Wait.
    /// </summary>
    [Fact]
    public void FencePool_AcquireSignaled_AfterReleasingUnsignaled_StillSignaled()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var pool     = new FencePool(device);

        var unsignaled = pool.Acquire(initiallySignaled: false);
        Assert.False(unsignaled.IsSignaled);
        pool.Release(unsignaled);

        var signaled = pool.Acquire(initiallySignaled: true);
        try
        {
            Assert.True(signaled.IsSignaled);
            Assert.Equal(WaitState.Signaled, signaled.Wait(TimeSpan.Zero));
        }
        finally { pool.Release(signaled); }
    }

    /// <summary>
    /// Release routes by current state — a signaled fence ends up on
    /// the signaled stack, an unsignaled one on the unsignaled stack —
    /// so subsequent Acquire calls honor <c>initiallySignaled</c>
    /// without growing the pool.
    /// </summary>
    [Fact]
    public void FencePool_Release_RoutesByFenceState()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var pool     = new FencePool(device);

        var signaled   = pool.Acquire(initiallySignaled: true);
        var unsignaled = pool.Acquire(initiallySignaled: false);
        Assert.Equal(2, pool.AllocatedCount);

        pool.Release(signaled);
        pool.Release(unsignaled);
        Assert.Equal(1, pool.IdleSignaledCount);
        Assert.Equal(1, pool.IdleUnsignaledCount);

        // Acquire(true) hits the signaled stack — same handle back, no growth.
        var s2 = pool.Acquire(initiallySignaled: true);
        unsafe { Assert.True(signaled.Handle == s2.Handle); }
        Assert.Equal(2, pool.AllocatedCount);
        Assert.True(s2.IsSignaled);

        var u2 = pool.Acquire(initiallySignaled: false);
        unsafe { Assert.True(unsignaled.Handle == u2.Handle); }
        Assert.Equal(2, pool.AllocatedCount);
        Assert.False(u2.IsSignaled);

        pool.Release(s2);
        pool.Release(u2);
    }

    /// <summary>
    /// The <c>Release(Fence, bool)</c> overload routes by the caller-supplied
    /// state without calling <c>vkGetFenceStatus</c> — the device-lost
    /// teardown path (issue #107) where the status query would itself throw.
    /// Proven by handing an unsignaled fence back as "known signaled": it
    /// lands on the signaled stack, which the status-querying overload could
    /// never do.
    /// </summary>
    [Fact]
    public void FencePool_Release_KnownState_SkipsStatusQuery()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var pool     = new FencePool(device);

        var unsignaled = pool.Acquire(initiallySignaled: false);
        Assert.False(unsignaled.IsSignaled);

        pool.Release(unsignaled, knownSignaled: true);
        Assert.Equal(1, pool.IdleSignaledCount);
        Assert.Equal(0, pool.IdleUnsignaledCount);
    }

    /// <summary>
    /// Discard destroys the underlying VkSemaphore and removes it from
    /// pool tracking — the escape hatch for stuck binary semaphores
    /// (signaled by AcquireNextImage but never waited-on by submit
    /// before Recreate, etc.). A subsequent AcquireBinary materializes
    /// a fresh handle rather than handing back the discarded one.
    /// </summary>
    [Fact]
    public void SemaphorePool_Discard_DestroysAndDropsTracking()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var pool     = new SemaphorePool(device);

        var first = pool.AcquireBinary();
        Assert.False(first.IsNull);
        Assert.Equal(1, pool.AllocatedCount);

        pool.Discard(first);
        Assert.Equal(0, pool.AllocatedCount);
        Assert.Equal(0, pool.IdleBinaryCount);

        // Fresh acquire must allocate a new handle: the free-list is
        // empty and tracking has no record of the discarded one, so
        // AllocatedCount rises from 0 back to 1. We deliberately do NOT
        // assert pointer inequality on first.Handle vs second.Handle —
        // the Vulkan spec doesn't promise that vkCreateSemaphore returns
        // a different address after a destroy, and at least one common
        // driver reuses the just-freed slot, which made this assertion
        // flake under load. AllocatedCount is the contract.
        var second = pool.AcquireBinary();
        Assert.False(second.IsNull);
        Assert.Equal(1, pool.AllocatedCount);

        pool.Release(second);
    }

    [Fact]
    public void SemaphorePool_Discard_AfterRelease_RemovesFromFreeList()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var pool     = new SemaphorePool(device);

        // Build a free-list with three binaries, then Discard the middle one.
        var a = pool.AcquireBinary();
        var b = pool.AcquireBinary();
        var c = pool.AcquireBinary();
        pool.Release(a);
        pool.Release(b);
        pool.Release(c);
        Assert.Equal(3, pool.IdleBinaryCount);

        pool.Discard(b);
        Assert.Equal(2, pool.AllocatedCount);
        Assert.Equal(2, pool.IdleBinaryCount);

        // The two surviving binaries should still be acquirable from
        // the free-list (no fresh allocations).
        var x = pool.AcquireBinary();
        var y = pool.AcquireBinary();
        Assert.Equal(2, pool.AllocatedCount);
        unsafe
        {
            // x and y are a and c in some order — neither is b.
            Assert.True(x.Handle != b.Handle);
            Assert.True(y.Handle != b.Handle);
        }

        pool.Release(x);
        pool.Release(y);
    }

    [Fact]
    public void SemaphorePool_Discard_ForeignHandle_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var poolA    = new SemaphorePool(device);
        using var poolB    = new SemaphorePool(device);

        var foreign = poolA.AcquireBinary();
        try
        {
            // poolB never produced this handle; Discard must reject
            // rather than silently destroying poolA's semaphore behind
            // poolA's back.
            Assert.Throws<ArgumentException>(() => poolB.Discard(foreign));
        }
        finally { poolA.Release(foreign); }
    }

    [Fact]
    public void SemaphorePool_BinaryAndTimeline_AreSeparateFreeLists()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var pool     = new SemaphorePool(device);

        var bin  = pool.AcquireBinary();
        var time = pool.AcquireTimeline();

        Assert.False(bin.IsNull);
        Assert.False(time.IsNull);
        Assert.Equal(2, pool.AllocatedCount);

        pool.Release(bin);
        pool.Release(time);
        Assert.Equal(1, pool.IdleBinaryCount);
        Assert.Equal(1, pool.IdleTimelineCount);

        // AcquireBinary must hit the binary free-list, not steal a timeline.
        var bin2 = pool.AcquireBinary();
        unsafe { Assert.True(bin.Handle == bin2.Handle); }
    }

    [Fact]
    public void TimelineSemaphore_Signal_AdvancesValue()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var pool     = new SemaphorePool(device);

        var sem = pool.AcquireTimeline();
        try
        {
            Assert.Equal(0UL, sem.Value);
            sem.Signal(7);
            Assert.Equal(7UL, sem.Value);
            Assert.Equal(WaitState.Signaled, sem.WaitFor(7, TimeSpan.Zero));
        }
        finally { pool.Release(sem); }
    }

    /// <summary>
    /// Regression (issue #108): a timeline recycled from the free-list resumes
    /// from its prior counter value — Vulkan cannot lower a timeline counter, so
    /// the pool does NOT reset it to 0. <c>AcquireTimeline</c> has no
    /// <c>initialValue</c> parameter precisely because silently ignoring one on
    /// the recycle path was the bug; callers read <c>Value</c> and track deltas.
    /// </summary>
    [Fact]
    public void AcquireTimeline_RecycledHandle_ResumesFromPriorCounterValue()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var pool     = new SemaphorePool(device);

        var first = pool.AcquireTimeline();
        Assert.Equal(0UL, first.Value);          // fresh create starts at 0
        first.Signal(42);
        Assert.Equal(42UL, first.Value);
        pool.Release(first);

        var second = pool.AcquireTimeline();
        // Recycled handle resumes from its prior counter — Vulkan cannot lower
        // a timeline counter, so it is NOT reset to 0. This is the documented
        // pooled-timeline contract (issue #108).
        unsafe { Assert.True(first.Handle == second.Handle); }
        Assert.Equal(42UL, second.Value);

        pool.Release(second);
    }

    [Fact]
    public void TimelineSemaphore_WaitFor_NeverSignaled_Times_Out()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var pool     = new SemaphorePool(device);

        var sem = pool.AcquireTimeline();
        try
        {
            Assert.Equal(WaitState.Timeout, sem.WaitFor(value: 999, TimeSpan.FromMilliseconds(1)));
        }
        finally { pool.Release(sem); }
    }

    [Fact]
    public void DefaultHandles_AreNull()
    {
        Fence f = default;
        Assert.True(f.IsNull);
        Assert.True(f.IsSignaled); // default is "nothing to wait on" → signaled.
        Assert.Equal(WaitState.Signaled, f.Wait(TimeSpan.Zero));

        BinarySemaphore b = default;
        Assert.True(b.IsNull);

        TimelineSemaphore t = default;
        Assert.True(t.IsNull);
    }

    private static Device CreateGraphicsDevice(Instance instance)
    {
        uint family = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    family = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }
}
