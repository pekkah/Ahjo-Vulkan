using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs acceptance criterion 2 of issue #7: PickPhysicalDevice round-trip
/// reports zero managed allocations after the ArrayPool is warm. The
/// picker delegate is <c>static</c> so the compiler caches a singleton
/// instance; allocation pressure shows up only if PickPhysicalDevice
/// itself allocates.
/// </summary>
[MemoryDiagnoser]
public class PhysicalDevicePickerBenchmark
{
    private Instance _instance = null!;

    [GlobalSetup]
    public void Setup()
    {
        _instance = Instance.Create(default);

        // Warm the ArrayPool by running one picker round-trip outside
        // the measured iterations — the very first call may inflate the
        // pool. Subsequent calls hit a parked buffer and report 0 B.
        _instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);
    }

    [GlobalCleanup]
    public void Cleanup() => _instance.Dispose();

    [Benchmark]
    public PhysicalDevice Pick() =>
        _instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);
}
