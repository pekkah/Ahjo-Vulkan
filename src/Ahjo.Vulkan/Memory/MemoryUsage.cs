namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VmaMemoryUsage</c>. Hint to VMA about how
/// the allocation will be used; the actual memory type is chosen by VMA
/// from the available heap/type pairs that satisfy the
/// <see cref="AllocationDescription"/> requirements.
/// </summary>
/// <remarks>
/// The classic <c>GpuOnly</c>/<c>CpuOnly</c>/<c>CpuToGpu</c>/<c>GpuToCpu</c>
/// values are intentionally omitted — VMA documents them as legacy and
/// recommends <see cref="Auto"/> + <c>HostAccess*</c> flags instead.
/// </remarks>
public enum MemoryUsage : uint
{
    Unknown          = 0,
    GpuLazilyAllocated = 6,
    Auto             = 7,
    AutoPreferDevice = 8,
    AutoPreferHost   = 9,
}
