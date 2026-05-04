using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Hot-path helpers around <see cref="VkResult"/>. The success path through
/// <see cref="ThrowIfFailed"/> compiles down to a single conditional branch
/// with no allocation; the failure path is in a cold helper marked
/// <see cref="MethodImplOptions.NoInlining"/> so the caller's inlining
/// budget isn't burned on code that runs once before the process dies.
/// </summary>
internal static class ResultExtensions
{
    // Catastrophic codes are pre-allocated so the failure path is also
    // zero-alloc — important when the failure is OOM and a fresh
    // allocation could itself fail. Re-throwing a cached exception gets
    // a fresh stack trace per throw via the runtime's dispatch info, so
    // the call site is still recoverable from the stack. The trade-off:
    // the Function field on cached instances is the generic literal
    // "vulkan call", not the [CallerMemberName] of the throwing site.
    // Callers that need the originating call site read it from the
    // stack trace; callers that just want the failure code read .Result.
    // Non-cached failure codes (everything except OOM_HOST/OOM_DEVICE/
    // DEVICE_LOST) allocate per call and carry the [CallerMemberName].
    private static readonly VulkanException OutOfHostMemory =
        new(VkResult.VK_ERROR_OUT_OF_HOST_MEMORY, "vulkan call");

    private static readonly VulkanException OutOfDeviceMemory =
        new(VkResult.VK_ERROR_OUT_OF_DEVICE_MEMORY, "vulkan call");

    private static readonly VulkanException DeviceLost =
        new(VkResult.VK_ERROR_DEVICE_LOST, "vulkan call");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSuccess(this VkResult result) => result == VkResult.VK_SUCCESS;

    /// <summary>
    /// Throws <see cref="VulkanException"/> if <paramref name="result"/> is
    /// anything other than <see cref="VkResult.VK_SUCCESS"/>. Use only on
    /// APIs whose contract has a single successful code; multi-code APIs
    /// (e.g. <c>vkAcquireNextImageKHR</c>) return the <see cref="VkResult"/>
    /// directly instead.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfFailed(this VkResult result, [CallerMemberName] string fn = "")
    {
        if (result != VkResult.VK_SUCCESS)
        {
            Throw(result, fn);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Throw(VkResult result, string fn)
    {
        throw result switch
        {
            VkResult.VK_ERROR_OUT_OF_HOST_MEMORY => OutOfHostMemory,
            VkResult.VK_ERROR_OUT_OF_DEVICE_MEMORY => OutOfDeviceMemory,
            VkResult.VK_ERROR_DEVICE_LOST => DeviceLost,
            _ => new VulkanException(result, fn),
        };
    }
}
