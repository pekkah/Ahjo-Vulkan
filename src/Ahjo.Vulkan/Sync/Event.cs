using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// A <c>VkEvent</c> used as a <b>split barrier</b>: signal it at the producer
/// with <see cref="CommandRecorder.SetEvent"/>, wait on it at the consumer
/// with <see cref="CommandRecorder.WaitEvent"/>, and the commands recorded
/// between the two overlap the hazard instead of stalling at a single
/// <see cref="CommandRecorder.PipelineBarrier"/>.
/// </summary>
/// <remarks>
/// <para><b>Ownership diverges from its <c>Sync/</c> neighbours.</b>
/// <see cref="Fence"/>, <see cref="BinarySemaphore"/> and
/// <see cref="TimelineSemaphore"/> report <see cref="OwnsHandle"/> as
/// <see langword="false"/> because <see cref="FencePool"/> /
/// <see cref="SemaphorePool"/> own their lifetime. <see cref="Event"/> is
/// caller-owned: it is minted by <see cref="Device.CreateEvent"/> and
/// destroys the <c>VkEvent</c> on <see cref="Dispose"/>. There is no
/// <c>EventPool</c> — a pool's value in this repo is routing by state, and
/// <c>vkGetEventStatus</c> is illegal on a device-only event
/// (<c>VUID-vkGetEventStatus-event-03940</c>), so a pool could not answer
/// "is this one still signaled?".</para>
/// <para><b>Lifetime.</b> Do not dispose while a submission that references
/// the event is still pending. <c>default(Event)</c> is a legal null handle
/// (<see cref="IsNull"/> is <see langword="true"/>, <see cref="Dispose"/> is
/// a no-op); double-dispose is undefined behavior — the standard handle
/// contract, see <see cref="IVulkanHandle{TSelf}"/>.</para>
/// <para><b><see cref="IsDeviceOnly"/> on a borrowed handle means
/// "unknown".</b> <see cref="FromRaw"/> and <c>default</c> carry
/// <see cref="EventCreateFlags.None"/> because the wrapper never learns a
/// borrowed event's create flags — read <see langword="false"/> as
/// <em>unknown</em>, never as <em>host-capable</em>.</para>
/// </remarks>
public readonly unsafe struct Event : IVulkanHandle<Event>, IDisposable
{
    public readonly VkEvent_T* Handle;
    internal readonly VkDevice_T* DeviceHandle;
    private readonly EventCreateFlags _flags;

    internal Event(VkEvent_T* handle, VkDevice_T* device, EventCreateFlags flags)
    {
        Handle       = handle;
        DeviceHandle = device;
        _flags       = flags;
        HandleRegistry.TrackCreate(this);
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_EVENT;
    public static Event FromRaw(nint handle) => new((VkEvent_T*)handle, null, EventCreateFlags.None);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    /// <inheritdoc/>
    public bool OwnsHandle => DeviceHandle != null;

    /// <summary>
    /// <see langword="true"/> when this event was created with
    /// <see cref="EventCreateFlags.DeviceOnly"/>. Always
    /// <see langword="false"/> for a borrowed (<see cref="FromRaw"/> /
    /// <c>default</c>) handle, where it means <em>unknown</em> rather than
    /// <em>host-capable</em>.
    /// </summary>
    public bool IsDeviceOnly => (_flags & EventCreateFlags.DeviceOnly) != 0;

    public void Dispose()
    {
        if (Handle == null) return;
        // FromRaw produces a borrowed handle with no DeviceHandle — the
        // caller owns the lifetime; calling vkDestroyEvent with a null
        // device handle would crash on every loader.
        if (!OwnsHandle) return;
        HandleRegistry.TrackDispose(this);
        Vk.vkDestroyEvent(DeviceHandle, Handle, null);
    }
}
