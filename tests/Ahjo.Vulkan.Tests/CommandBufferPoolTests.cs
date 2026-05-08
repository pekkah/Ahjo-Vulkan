using System.Threading;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class CommandBufferPoolTests
{
    [Fact]
    public void Begin_End_Allocates_Once()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint family = PickGraphicsFamily(instance, out var gpu);

        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });

        using var pool = new CommandBufferPool(device, family);

        Assert.Equal(family, pool.QueueFamilyIndex);
        Assert.Equal(0, pool.AllocatedCount);

        using (var rec = pool.Begin())
        {
            Assert.False(rec.IsNull);
            Assert.Equal(1, pool.OutstandingCount);
        }

        Assert.Equal(0, pool.OutstandingCount);
        Assert.Equal(1, pool.AllocatedCount);
    }

    [Fact]
    public void Begin_Twice_AllocatesTwo_BeforeReset()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint family = PickGraphicsFamily(instance, out var gpu);
        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
        using var pool = new CommandBufferPool(device, family);

        // No reset between Begin/Dispose pairs — the spent buffer can't be
        // reused this frame, so the pool grows on the second Begin.
        using (pool.Begin()) { }
        using (pool.Begin()) { }

        Assert.Equal(2, pool.AllocatedCount);
    }

    [Fact]
    public void ResetForFrame_RecyclesSpentBuffers()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint family = PickGraphicsFamily(instance, out var gpu);
        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
        using var pool = new CommandBufferPool(device, family);

        // Frame 1: warmup.
        using (pool.Begin()) { }
        using (pool.Begin()) { }
        int afterFrame1 = pool.AllocatedCount;

        pool.ResetForFrame();

        // Frame 2: same number of begins should hit the recycle path.
        using (pool.Begin()) { }
        using (pool.Begin()) { }
        Assert.Equal(afterFrame1, pool.AllocatedCount);
    }

    [Fact]
    public void RecordOnThreadA_SubmitOnThreadB_FenceSignals()
    {
        // CommandBufferPool's xmldoc spells out "one pool per (queue
        // family × thread)" — same pool from two threads is not safe;
        // different pools sharing one device + queue is. Pin the latter
        // contract: thread A owns pool A and records, thread B submits
        // the recorded buffer by raw handle (CommandRecorder is a
        // ref struct so it can't cross threads, but the VkCommandBuffer
        // pointer can — see Queue.Submit2(nint, in Fence)).
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        // vkQueueSubmit2 SIGSEGVs inside Mesa lavapipe ~17 tests' worth
        // (see project_lavapipe_vma_segfault memory) — gate the same
        // way the rest of the queue-submitting suite does.
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software driver — vkQueueSubmit2 SIGSEGV on lavapipe; gated.");

        using var instance = Instance.Create(default);
        uint family = PickGraphicsFamily(instance, out var gpu);
        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });

        // Two separate pools — Vulkan's external-sync rule on VkCommandPool
        // means each thread that records needs its own. Only pool A actually
        // records here; pool B exists to make the per-thread shape obvious
        // even though thread B does no recording.
        using var poolA = new CommandBufferPool(device, family);
        using var poolB = new CommandBufferPool(device, family);

        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();

        nint cmdHandle = 0;
        Exception? threadAFailure = null;
        Exception? threadBFailure = null;

        // recorderReady → A has finished recording, handle is ready.
        // submitDone   → B has finished submitting + waiting on the fence.
        // A holds the recorder undisposed across submitDone so the cb
        // stays in the pool's outstanding set; otherwise A could race
        // ahead and ResetForFrame the cb out from under B.
        using var recorderReady = new ManualResetEventSlim(false);
        using var submitDone    = new ManualResetEventSlim(false);

        var threadA = new Thread(() =>
        {
            try
            {
                using var rec = poolA.Begin();
                // Empty cmd buffer is a valid Vulkan submission — no
                // commands needed to exercise the cross-thread submit
                // path. The fence still signals once GPU execution
                // completes.
                rec.End();
                cmdHandle = rec.RawHandle;
                recorderReady.Set();
                if (!submitDone.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Thread B did not signal submitDone within 10s.");
            }
            catch (Exception ex)
            {
                threadAFailure = ex;
                recorderReady.Set(); // unblock B so it doesn't deadlock
            }
        }) { IsBackground = true, Name = "ThreadA-record" };

        var threadB = new Thread(() =>
        {
            try
            {
                if (!recorderReady.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Thread A did not signal recorderReady within 10s.");
                if (threadAFailure is not null) return;

                var queue = device.GetQueue(family, 0);
                queue.Submit2(cmdHandle, in fence);
                Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(10)));
            }
            catch (Exception ex)
            {
                threadBFailure = ex;
            }
            finally
            {
                submitDone.Set();
            }
        }) { IsBackground = true, Name = "ThreadB-submit" };

        threadA.Start();
        threadB.Start();
        Assert.True(threadA.Join(TimeSpan.FromSeconds(15)), "Thread A did not finish in time.");
        Assert.True(threadB.Join(TimeSpan.FromSeconds(15)), "Thread B did not finish in time.");

        if (threadAFailure is not null) throw new Xunit.Sdk.XunitException("Thread A failed", threadAFailure);
        if (threadBFailure is not null) throw new Xunit.Sdk.XunitException("Thread B failed", threadBFailure);

        // Pool A handed out exactly one buffer; pool B was unused.
        Assert.Equal(1, poolA.AllocatedCount);
        Assert.Equal(0, poolB.AllocatedCount);

        fencePool.Release(fence);
    }

    [Fact]
    public void Begin_AfterDispose_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint family = PickGraphicsFamily(instance, out var gpu);
        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });

        var pool = new CommandBufferPool(device, family);
        pool.Dispose();

        Assert.Throws<ObjectDisposedException>(() => pool.Begin());
    }

    private static uint PickGraphicsFamily(Instance instance, out PhysicalDevice gpu)
    {
        uint family = uint.MaxValue;
        gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
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
        return family;
    }
}
