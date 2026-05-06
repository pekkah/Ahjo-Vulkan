using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs acceptance criterion 1 of issue 16: 100-frame loop reports zero
/// allocations per frame after warmup. The benchmark drives a real
/// CPU-only iteration of the per-frame path — BeginFrame waits the
/// slot's fence (signaled at construction so the wait is immediate),
/// resets pools, returns a context, then submits a no-op command buffer
/// that the GPU completes within the same iteration's measurement.
/// </summary>
[MemoryDiagnoser]
public class FrameRingBenchmarks
{
    private Instance     _instance = null!;
    private Device       _device   = null!;
    private FrameRing    _ring     = null!;
    private Queue        _queue    = null!;

    [GlobalSetup]
    public void Setup()
    {
        _instance = Instance.Create(default);

        uint family = uint.MaxValue;
        var gpu = _instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
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
        _device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
        _ring  = new FrameRing(_device, framesInFlight: 2, queueFamily: family);
        _queue = _device.GetQueue(family, 0);

        // Warm: run two full passes through the ring so every slot has
        // its command pool warm + an outstanding fence.
        for (int i = 0; i < 4; i++)
        {
            using var frame = _ring.BeginFrame();
            var rec = frame.CommandBuffers.Begin();
            frame.Submit(_queue, ref rec);
            rec.Dispose();
            frame.InFlight.Wait(TimeSpan.FromSeconds(5));
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _ring.Dispose();
        _device.Dispose();
        _instance.Dispose();
    }

    [Benchmark]
    public ulong Frame_Begin_Submit_Wait()
    {
        using var frame = _ring.BeginFrame();
        var rec = frame.CommandBuffers.Begin();
        frame.Submit(_queue, ref rec);
        rec.Dispose();
        // Wait inline so the next iteration's BeginFrame doesn't have to
        // — keeps the measurement focused on the per-frame surface, not
        // on whichever slot's fence happens to be racing.
        frame.InFlight.Wait(TimeSpan.FromSeconds(1));
        return frame.FrameNumber;
    }
}
