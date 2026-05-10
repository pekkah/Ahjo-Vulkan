using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs the <c>Buffer.Map_AsSpan</c> entry of issue 29: a persistent-mapped
/// host-visible buffer should expose its backing memory as a
/// <see cref="Span{T}"/> with 0 B/op after warmup. The benchmark goes through
/// <see cref="Buffer.AsSpan{T}"/> rather than <see cref="Buffer.Map{T}"/>
/// because <c>Map</c> instantiates a <see cref="MappedRegion{T}"/> class —
/// that allocation is a property of the <c>MemoryManager{T}</c>-shaped API,
/// not the hot path. <c>AsSpan</c> is the alloc-free read/write surface the
/// engine uses for per-frame uniform updates.
/// </summary>
[MemoryDiagnoser]
public class BufferBenchmarks
{
    private const int CallsPerInvoke = 1024;

    private Instance _instance = null!;
    private Device   _device   = null!;
    private Buffer   _buffer;

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

        _buffer = _device.Allocator.CreateBuffer(
            new BufferDescription { Size = 4096, Usage = BufferUsage.UniformBuffer },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _buffer.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public int Map_AsSpan()
    {
        int sum = 0;
        for (int i = 0; i < CallsPerInvoke; i++)
        {
            Span<int> span = _buffer.AsSpan<int>();
            span[0] = i;
            sum += span[0];
        }
        return sum;
    }
}
