using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs acceptance criterion 2 of issue 34: steady-state
/// <see cref="StagingUploader.Upload{T}"/> allocates 0 B per op after
/// warmup. The benchmark resets the uploader inside the iteration so
/// each call sees a fresh head — i.e. mirrors what a frame loop sees
/// after a <c>Reset</c> at frame begin.
/// </summary>
[MemoryDiagnoser]
public class StagingUploaderBenchmarks
{
    private Instance         _instance = null!;
    private Device           _device   = null!;
    private StagingUploader  _uploader = null!;
    private float[]          _payload  = null!;

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
        _device   = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
        _uploader = new StagingUploader(_device.Allocator);
        _payload  = new float[1024]; // 4 KiB

        // Warm — first upload grows the pool by one chunk; subsequent
        // resets/uploads hit the same chunk and must be alloc-free.
        _uploader.Upload<float>(_payload);
        _uploader.Reset();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _uploader?.Dispose();
        _device.Dispose();
        _instance.Dispose();
    }

    [Benchmark]
    public ulong Upload_4KiB_Float()
    {
        _uploader.Reset();
        var u = _uploader.Upload<float>(_payload);
        return u.Size;
    }
}
