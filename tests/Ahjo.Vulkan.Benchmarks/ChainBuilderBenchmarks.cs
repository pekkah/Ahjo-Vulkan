using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs acceptance criterion #4 of issue 03: BenchmarkDotNet harness shows
/// zero allocations in the round-trip path. Three nodes — features2 →
/// vulkan13 → vulkan12 — matches the typical "device features pNext chain"
/// shape on a modern engine.
/// </summary>
[MemoryDiagnoser]
public class ChainBuilderBenchmarks
{
    [Benchmark]
    [SkipLocalsInit]
    public int BuildThreeNodeChain()
    {
        Span<byte> scratch = stackalloc byte[1024];
        var chain = ChainBuilder.From(scratch);
        chain.Root<VkPhysicalDeviceFeatures2>(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2);
        chain.Push<VkPhysicalDeviceVulkan13Features>(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_3_FEATURES);
        chain.Push<VkPhysicalDeviceVulkan12Features>(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_2_FEATURES);
        return chain.Length;
    }
}
