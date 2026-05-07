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
    /// Maps the success-set of <c>vkWaitForFences</c> and
    /// <c>vkWaitSemaphores</c> into <see cref="WaitState"/>. Spec-legal
    /// returns for these wait APIs are <c>SUCCESS</c>, <c>TIMEOUT</c>, and
    /// <c>ERROR_DEVICE_LOST</c> (plus the OOM error codes, which throw).
    /// <c>VK_NOT_READY</c> is intentionally not mapped: it is returned
    /// only by the non-blocking status APIs (<c>vkGetFenceStatus</c>,
    /// <c>vkGetEventStatus</c>) and those have their own dedicated paths
    /// (see <see cref="Fence.IsSignaled"/>) — surfacing it through this
    /// mapper would silently mask a wrong-API call as a timeout.
    /// </summary>
    public static WaitState ToWaitState(this VkResult result) => result switch
    {
        VkResult.VK_SUCCESS           => WaitState.Signaled,
        VkResult.VK_TIMEOUT           => WaitState.Timeout,
        VkResult.VK_ERROR_DEVICE_LOST => WaitState.DeviceLost,
        _                             => throw new VulkanException(result, "wait"),
    };

    /// <summary>
    /// Converts a <see cref="TimeSpan"/> to the nanosecond timeout Vulkan
    /// wait APIs expect. Negative spans collapse to <c>UInt64.MaxValue</c>
    /// (wait forever) for symmetry with <c>Timeout.InfiniteTimeSpan</c>.
    /// <see cref="TimeSpan.Zero"/> maps to <c>0</c> nanoseconds, which the
    /// Vulkan spec defines as a non-blocking poll: the call returns
    /// immediately with <c>VK_TIMEOUT</c> if the fence/semaphore is not
    /// already signaled. Callers that want "wait forever" must pass
    /// <see cref="Timeout.InfiniteTimeSpan"/> (or any negative span).
    /// </summary>
    public static ulong ToVulkanTimeout(this TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero) return ulong.MaxValue;
        // TimeSpan.Ticks is 100ns per tick → multiply by 100 for nanoseconds.
        long ticks = timeout.Ticks;
        return ticks > (long.MaxValue / 100) ? ulong.MaxValue : (ulong)(ticks * 100);
    }
}
