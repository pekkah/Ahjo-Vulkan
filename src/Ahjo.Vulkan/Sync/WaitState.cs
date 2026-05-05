using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Outcome of a wait on a <see cref="Fence"/> or <see cref="TimelineSemaphore"/>.
/// Collapses the <see cref="VkResult"/> success-set into the three cases
/// engine code actually branches on.
/// </summary>
public enum WaitState
{
    Signaled,
    Timeout,
    DeviceLost,
}

internal static class WaitStateExtensions
{
    /// <summary>
    /// Maps the success-set of <c>vkWaitForFences</c> /
    /// <c>vkWaitSemaphores</c> / <c>vkGetFenceStatus</c> into
    /// <see cref="WaitState"/>. Throws <see cref="VulkanException"/> for
    /// any other code so the caller doesn't have to remember the wait-API
    /// quirks (only <c>SUCCESS</c>, <c>TIMEOUT</c>, <c>NOT_READY</c>, and
    /// <c>ERROR_DEVICE_LOST</c> are spec-legal here).
    /// </summary>
    public static WaitState ToWaitState(this VkResult result) => result switch
    {
        VkResult.VK_SUCCESS           => WaitState.Signaled,
        VkResult.VK_TIMEOUT           => WaitState.Timeout,
        VkResult.VK_NOT_READY         => WaitState.Timeout,
        VkResult.VK_ERROR_DEVICE_LOST => WaitState.DeviceLost,
        _                             => throw new VulkanException(result, "wait/signal"),
    };

    /// <summary>
    /// Converts a <see cref="TimeSpan"/> to the nanosecond timeout Vulkan
    /// wait APIs expect. Negative spans collapse to <c>UInt64.MaxValue</c>
    /// (wait forever) for symmetry with <c>Timeout.InfiniteTimeSpan</c>.
    /// </summary>
    public static ulong ToVulkanTimeout(this TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero) return ulong.MaxValue;
        // TimeSpan.Ticks is 100ns per tick → multiply by 100 for nanoseconds.
        long ticks = timeout.Ticks;
        return ticks > (long.MaxValue / 100) ? ulong.MaxValue : (ulong)(ticks * 100);
    }
}
