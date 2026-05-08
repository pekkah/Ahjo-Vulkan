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
}
