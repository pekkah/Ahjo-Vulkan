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
    /// directly, or — for the <c>count → fill</c> two-call idiom — use
    /// <see cref="ThrowIfErrored"/>.
    /// </summary>
    /// <remarks>
    /// "Single successful code" is no longer a doc-comment honour system: the
    /// <c>ResultPolicyGuardTests</c> guard test (issue #117) scans the wrapper
    /// for <c>vk…().ThrowIfFailed()</c> and fails the build on any command that
    /// vk.xml lists with more than one <c>successcode</c>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfFailed(this VkResult result, [CallerMemberName] string fn = "")
    {
        if (result != VkResult.VK_SUCCESS)
        {
            Throw(result, fn);
        }
    }

    /// <summary>
    /// Throws <see cref="VulkanException"/> only when <paramref name="result"/>
    /// is an <em>error</em> code (negative <see cref="VkResult"/>); any of the
    /// non-error success codes — <c>VK_SUCCESS</c>, <c>VK_INCOMPLETE</c>,
    /// <c>VK_SUBOPTIMAL_KHR</c>, <c>VK_TIMEOUT</c>, <c>VK_NOT_READY</c>, … —
    /// pass through and are returned to the caller to branch on.
    /// </summary>
    /// <remarks>
    /// This is the correct guard for the second (fill) call of the two-call
    /// <c>count → fill</c> idiom and other multi-success-code entry points:
    /// the underlying set can legally grow between the size query and the
    /// fill, and the driver signals that with <c>VK_INCOMPLETE</c> — a
    /// spec-defined success, not a failure (issue #97). Vulkan encodes every
    /// error as a negative <see cref="VkResult"/> and every success/partial
    /// outcome as non-negative, so the sign test is the spec's own partition.
    /// The <c>ResultPolicyGuardTests</c> guard test (issue #117) enforces that
    /// multi-success commands use this helper instead of
    /// <see cref="ThrowIfFailed"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfErrored(this VkResult result, [CallerMemberName] string fn = "")
    {
        if ((int)result < 0)
        {
            Throw(result, fn);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Throw(VkResult result, string fn)
    {
        // Single choke point for device loss (issue #120): every
        // ThrowIfFailed/ThrowIfErrored failure funnels through here, so a
        // DEVICE_LOST marks Device.IsLost before the throw — wait/status
        // fast paths and teardown policy all key off that flag instead of
        // each call site deciding its own post-loss behavior. The throw
        // site carries no device identity, so the notification marks every
        // live device (exact in the one-device-per-process target shape;
        // see Device.IsLost remarks for the multi-device caveat).
        if (result == VkResult.VK_ERROR_DEVICE_LOST)
            Device.NotifyDeviceLossObserved();

        throw result switch
        {
            VkResult.VK_ERROR_OUT_OF_HOST_MEMORY => OutOfHostMemory,
            VkResult.VK_ERROR_OUT_OF_DEVICE_MEMORY => OutOfDeviceMemory,
            VkResult.VK_ERROR_DEVICE_LOST => DeviceLost,
            _ => new VulkanException(result, fn),
        };
    }

    /// <summary>
    /// The cached device-lost exception, exposed so device-context call
    /// sites (<see cref="Fence.IsSignaled"/>, <see cref="Swapchain.Recreate"/>)
    /// can fail deterministically after <see cref="Device.IsLost"/> without
    /// allocating. Re-throwing gets a fresh stack trace per throw via the
    /// runtime's dispatch info.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowDeviceLost() => throw DeviceLost;
}
