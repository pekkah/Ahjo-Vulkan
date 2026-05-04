using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs acceptance criterion #2 of issue 04: BenchmarkDotNet test confirms
/// zero allocations on a 1M-iteration tight loop of a successful operation.
/// </summary>
[MemoryDiagnoser]
public class ResultPolicyBenchmarks
{
    [Benchmark(OperationsPerInvoke = 1_000_000)]
    public void ThrowIfFailed_Success_TightLoop()
    {
        for (var i = 0; i < 1_000_000; i++)
        {
            VkResult.VK_SUCCESS.ThrowIfFailed();
        }
    }

    [Benchmark(OperationsPerInvoke = 1_000_000)]
    public int IsSuccess_TightLoop()
    {
        var trues = 0;
        for (var i = 0; i < 1_000_000; i++)
        {
            if (VkResult.VK_SUCCESS.IsSuccess()) trues++;
        }
        return trues;
    }
}
