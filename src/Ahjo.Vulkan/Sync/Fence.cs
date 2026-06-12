using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// CPU/GPU sync primitive over a <c>VkFence</c>. <c>readonly struct</c> —
/// copy-by-value, two pointers (the fence handle + the device handle that
/// owns it). Lifetime is owned by <see cref="FencePool"/>; do not call
/// <c>vkDestroyFence</c> directly. Pass back through
/// <see cref="FencePool.Release"/> when the wait/reset cycle is done.
/// </summary>
/// <remarks>
/// <c>default(Fence)</c> is a legal null handle: <see cref="IsNull"/> is
/// <see langword="true"/>, every method is a no-op or returns
/// <see cref="WaitState.Signaled"/> (treating "no fence" as
/// "nothing to wait for"). Double-release is undefined behavior — the
/// pool relies on every fence appearing in its free-list at most once.
/// </remarks>
public readonly unsafe struct Fence : IVulkanHandle<Fence>
{
    public readonly VkFence_T*  Handle;
    internal readonly VkDevice_T* DeviceHandle;
    // The managed Device wrapper, when known (pool-acquired fences) — the
    // only way a raw-pointer struct can read Device.IsLost (#120). Null on
    // FromRaw/default and on raw-pointer test ctors; when non-null it must
    // be the Device that owns DeviceHandle. A managed reference field is
    // legal since #118 relaxed IVulkanHandle to `struct`.
    internal readonly Device?     Owner;

    internal Fence(VkFence_T* handle, VkDevice_T* device)
    {
        Handle       = handle;
        DeviceHandle = device;
        Owner        = null;
    }

    internal Fence(VkFence_T* handle, Device owner)
    {
        Handle       = handle;
        DeviceHandle = owner.Handle;
        Owner        = owner;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_FENCE;
    public static Fence FromRaw(nint handle) => new((VkFence_T*)handle, (VkDevice_T*)null);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    /// <summary>
    /// Always <see langword="false"/>: <see cref="FencePool"/> owns the
    /// <c>VkFence</c>'s lifetime; the struct never destroys it.
    /// </summary>
    public bool OwnsHandle => false;

    /// <summary>
    /// Cheap, non-blocking signaled check. Wraps <c>vkGetFenceStatus</c>;
    /// returns <see langword="true"/> on <c>VK_SUCCESS</c> and
    /// <see langword="false"/> on <c>VK_NOT_READY</c>. Any other code
    /// throws.
    /// </summary>
    /// <remarks>
    /// Cannot reuse <see cref="WaitStateExtensions.ToWaitState"/>: that
    /// mapping is for the wait APIs (vkWaitForFences / vkWaitSemaphores),
    /// whose success-set is <c>SUCCESS</c> / <c>TIMEOUT</c> /
    /// <c>ERROR_DEVICE_LOST</c> with <c>VK_NOT_READY</c> intentionally
    /// excluded. <c>vkGetFenceStatus</c>'s success-set is
    /// <c>SUCCESS</c> / <c>NOT_READY</c> / <c>ERROR_DEVICE_LOST</c>;
    /// folding both into one mapper would silently treat a wrong-API
    /// <c>NOT_READY</c> as a timeout and vice versa.
    /// </remarks>
    public bool IsSignaled
    {
        get
        {
            if (Handle == null) return true;
            ThrowIfBorrowed();
            // After device loss the fence state is unknowable; throw the
            // cached DeviceLost without calling the driver. Deterministic
            // version of the existing contract (anything outside
            // SUCCESS/NOT_READY throws) — teardown paths that must not
            // throw consult Device.IsLost at the pool layer instead
            // (FencePool.Release, issue #120).
            if (Owner is { IsLost: true })
                ResultExtensions.ThrowDeviceLost();
            VkResult r = Vk.vkGetFenceStatus(DeviceHandle, Handle);
            if (r == VkResult.VK_ERROR_DEVICE_LOST)
                Owner?.MarkLost();
            return r switch
            {
                VkResult.VK_SUCCESS   => true,
                VkResult.VK_NOT_READY => false,
                _                     => throw new VulkanException(r, "vkGetFenceStatus"),
            };
        }
    }

    /// <summary>
    /// Blocks until the fence is signaled or <paramref name="timeout"/>
    /// elapses. Pass <see cref="Timeout.InfiniteTimeSpan"/> (or any
    /// negative span) to wait forever; pass <see cref="TimeSpan.Zero"/>
    /// to poll (returns <see cref="WaitState.Timeout"/> immediately if
    /// not yet signaled, matching <c>vkWaitForFences</c>'s 0-ns poll
    /// semantics — use <see cref="IsSignaled"/> when polling intent
    /// should be self-documenting).
    /// </summary>
    public WaitState Wait(TimeSpan timeout)
    {
        if (Handle == null) return WaitState.Signaled;
        ThrowIfBorrowed();
        // Post-loss waits return immediately (#120): the fence may never
        // signal, and some drivers historically stall infinite waits after
        // a TDR. One null check + one volatile read on the healthy path,
        // in front of a host syscall.
        if (Owner is { IsLost: true }) return WaitState.DeviceLost;
        VkFence_T* h = Handle;
        WaitState state = Vk.vkWaitForFences(DeviceHandle, 1, &h, waitAll: 1, timeout.ToVulkanTimeout()).ToWaitState();
        if (state == WaitState.DeviceLost)
            Owner?.MarkLost();
        return state;
    }

    /// <summary>
    /// Resets the fence back to the unsignaled state. Caller's responsibility
    /// to ensure no GPU work is currently waiting on this fence's signal —
    /// resetting a fence with pending submits is a validation error.
    /// </summary>
    public void Reset()
    {
        if (Handle == null) return;
        ThrowIfBorrowed();
        VkFence_T* h = Handle;
        Vk.vkResetFences(DeviceHandle, 1, &h).ThrowIfFailed();
    }

    // FromRaw produces a borrowed fence with no DeviceHandle; dispatching
    // through it would dereference the loader's null dispatch table and
    // access-violate the process (issue #102). Fail loudly instead.
    private void ThrowIfBorrowed()
    {
        if (DeviceHandle == null)
            throw new InvalidOperationException(
                "Fence requires an owning device for status/wait/reset calls; " +
                "a FromRaw-constructed (borrowed) fence has none.");
    }
}
