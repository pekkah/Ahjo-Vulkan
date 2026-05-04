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
    // Catastrophic codes are pre-allocated. Re-throwing a cached exception
    // gets a fresh stack trace per throw via the runtime's dispatch info,
    // and the Function field is generic ("vulkan call") since the call
    // site is recoverable from the stack — losing the per-call name is
    // a fair trade for zero allocation on OOM / device-lost paths where
    // allocation might itself fail.
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
