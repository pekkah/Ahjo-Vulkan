using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs acceptance criterion 1 of issue 38: every sync-primitive pool
/// (<see cref="FencePool"/>, <see cref="SemaphorePool"/> binary,
/// <see cref="SemaphorePool"/> timeline) reports <b>0 B per acquire/release
/// pair</b> after warmup. The pools are pre-warmed in
/// <see cref="Setup"/> so the steady-state ping-pong hits the free-list
/// instead of growing it; the inner loop runs <c>CallsPerInvoke</c>
/// pairs per invocation so BDN's per-op allocation column converges.
/// </summary>
[MemoryDiagnoser]
public class SyncPoolBenchmarks
{
    private const int CallsPerInvoke = 1000;

    private Instance      _instance      = null!;
    private Device        _device        = null!;
    private FencePool     _fencePool     = null!;
    private SemaphorePool _semaphorePool = null!;

    [GlobalSetup]
    public void Setup()
    {
        _instance = Instance.Create(default);

        uint family = uint.MaxValue;
        var gpu = _instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsCompute)
                {
                    family = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        _device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });

        _fencePool     = new FencePool(_device);
        _semaphorePool = new SemaphorePool(_device);

        // Warm — fault in one of each kind so steady-state Acquire/Release
        // ping-pongs against a populated free-list rather than growing it.
        var f = _fencePool.Acquire();
        _fencePool.Release(f);

        var b = _semaphorePool.AcquireBinary();
        _semaphorePool.Release(b);

        var t = _semaphorePool.AcquireTimeline();
        _semaphorePool.Release(t);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _semaphorePool?.Dispose();
        _fencePool?.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void Fence_AcquireRelease()
    {
        for (int i = 0; i < CallsPerInvoke; i++)
        {
            var f = _fencePool.Acquire();
            _fencePool.Release(f);
        }
    }

    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void Semaphore_Binary_AcquireRelease()
    {
        for (int i = 0; i < CallsPerInvoke; i++)
        {
            var s = _semaphorePool.AcquireBinary();
            _semaphorePool.Release(s);
        }
    }

    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void Semaphore_Timeline_AcquireRelease()
    {
        for (int i = 0; i < CallsPerInvoke; i++)
        {
            var s = _semaphorePool.AcquireTimeline();
            _semaphorePool.Release(s);
        }
    }

    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public ulong Sync_HostOps_RoundTrip()
    {
        // The host-side per-frame sync surface: Fence.IsSignaled/Wait/Reset
        // and TimelineSemaphore.Signal/WaitFor/Value all carry the borrowed-
        // handle guard added by #102/#118 and the Device.IsLost fast-path
        // read added by #120 — this keeps both branches under the 0 B/op
        // canary. Wait(Zero) on the unsignaled fence is a non-blocking poll
        // (returns Timeout immediately), so the Wait guard is exercised
        // without a queue submit. Acquire/Release bracket the ops so the
        // pool returns to steady state each iteration.
        ulong sum = 0;
        for (int i = 0; i < CallsPerInvoke; i++)
        {
            var f = _fencePool.Acquire();
            if (f.IsSignaled) sum++;
            if (f.Wait(TimeSpan.Zero) == WaitState.Timeout) sum++;
            f.Reset();
            _fencePool.Release(f);

            var t = _semaphorePool.AcquireTimeline();
            ulong next = t.Value + 1;
            t.Signal(next);
            t.WaitFor(next, TimeSpan.Zero);
            sum += t.Value;
            _semaphorePool.Release(t);
        }
        return sum;
    }
}
