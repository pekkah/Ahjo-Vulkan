using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// #244 canary: the no-fault per-frame <see cref="Device.TryPollDeviceFaults"/>
/// is one <c>vkGetDeviceFaultReportsKHR</c> count call answering
/// <c>VK_TIMEOUT</c>, and must allocate nothing.
/// </summary>
/// <remarks>
/// Driverless, the <see cref="ResultPolicyBenchmarks"/> shape: an
/// <c>[UnmanagedCallersOnly]</c> fake stands in for the driver. The
/// <see cref="Device"/> layer is covered on KHR hardware by
/// <c>DeviceFaultTests.Poll_NoFault_IsZeroAllocation</c> and the contended-lock
/// test, whose exact-zero allocation assertion was chosen over a host-gated
/// device-level benchmark class as the stronger check.
/// </remarks>
[MemoryDiagnoser]
public unsafe class DeviceFaultPollBenchmarks
{
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static VkResult FakeReportsNoFault(VkDevice_T* device, ulong timeout, uint* pCount, VkDeviceFaultInfoKHR* pInfo)
    {
        *pCount = 0;
        return VkResult.VK_TIMEOUT;
    }

    [Benchmark(OperationsPerInvoke = 1024)]
    public int ReadKhrEntries_NoFault_1024()
    {
        int total = 0;
        for (int i = 0; i < 1024; i++)
        {
            DeviceFaultReader.ReadKhrEntries(&FakeReportsNoFault, (VkDevice_T*)0x1, 0,
                out DeviceFaultEntry[] entries, out _);
            total += entries.Length;
        }
        return total;
    }
}
