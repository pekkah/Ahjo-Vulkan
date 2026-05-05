using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// 64-bit-counter semaphore — the cross-frame, cross-queue ordering
/// primitive for Vulkan 1.4. <c>readonly struct</c>; copy-by-value;
/// lifetime owned by <see cref="SemaphorePool"/>. Unlike
/// <see cref="BinarySemaphore"/> the CPU can both <see cref="Signal"/>
/// and <see cref="WaitFor"/> directly, which is the basis for the
/// polled-future pattern in the architecture spec.
/// </summary>
public readonly unsafe struct TimelineSemaphore : IVulkanHandle<TimelineSemaphore>
{
    public readonly VkSemaphore_T* Handle;
    internal readonly VkDevice_T*  DeviceHandle;

    internal TimelineSemaphore(VkSemaphore_T* handle, VkDevice_T* device)
    {
        Handle       = handle;
        DeviceHandle = device;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_SEMAPHORE;
    public static TimelineSemaphore FromRaw(nint handle) => new((VkSemaphore_T*)handle, null);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    /// <summary>Current counter value via <c>vkGetSemaphoreCounterValue</c>.</summary>
    public ulong Value
    {
        get
        {
            ulong v = 0;
            Vk.vkGetSemaphoreCounterValue(DeviceHandle, Handle, &v).ThrowIfFailed();
            return v;
        }
    }

    /// <summary>
    /// CPU-side signal. The GPU can also signal a timeline semaphore via
    /// <c>VkSubmitInfo2</c>; both paths advance the same counter.
    /// <paramref name="value"/> must be strictly greater than the current
    /// value (Vulkan rejects monotonicity violations).
    /// </summary>
    public void Signal(ulong value)
    {
        var info = new VkSemaphoreSignalInfo
        {
            sType     = VkStructureType.VK_STRUCTURE_TYPE_SEMAPHORE_SIGNAL_INFO,
            semaphore = Handle,
            value     = value,
        };
        Vk.vkSignalSemaphore(DeviceHandle, &info).ThrowIfFailed();
    }

    /// <summary>
    /// Blocks the calling thread until the counter reaches at least
    /// <paramref name="value"/> or <paramref name="timeout"/> elapses.
    /// Pass <see cref="Timeout.InfiniteTimeSpan"/> (or any negative span)
    /// to wait forever.
    /// </summary>
    public WaitState WaitFor(ulong value, TimeSpan timeout)
    {
        VkSemaphore_T* h = Handle;
        var info = new VkSemaphoreWaitInfo
        {
            sType          = VkStructureType.VK_STRUCTURE_TYPE_SEMAPHORE_WAIT_INFO,
            semaphoreCount = 1,
            pSemaphores    = &h,
            pValues        = &value,
        };
        return Vk.vkWaitSemaphores(DeviceHandle, &info, timeout.ToVulkanTimeout()).ToWaitState();
    }
}
