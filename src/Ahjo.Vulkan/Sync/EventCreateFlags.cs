namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkEventCreateFlagBits</c> (Vulkan 1.3 core).
/// Selects which of the two mutually exclusive event usage modes an
/// <see cref="Event"/> is created for.
/// </summary>
/// <remarks>
/// <para><see cref="DeviceOnly"/> is the default for split barriers
/// (<see cref="CommandRecorder.SetEvent"/> /
/// <see cref="CommandRecorder.WaitEvent"/>) and is what
/// <see cref="Device.CreateEvent"/> passes unless told otherwise. It makes
/// the host event commands illegal on the event: <c>vkSetEvent</c>
/// (<c>VUID-vkSetEvent-event-03941</c>), <c>vkGetEventStatus</c>
/// (<c>VUID-vkGetEventStatus-event-03940</c>) and <c>vkResetEvent</c>
/// (<c>VUID-vkResetEvent-event-03823</c>) must not be called on a
/// device-only event.</para>
/// <para><see cref="None"/> therefore exists even though the wrapper exposes
/// no host event operations today: the flag is the switch between the two
/// modes, not a tuning knob, so a caller that will later need host-side
/// event access has to opt out of device-only at create time.</para>
/// </remarks>
[Flags]
public enum EventCreateFlags : uint
{
    /// <summary>
    /// No flags — a host-capable event. Required if the event will ever be
    /// touched by <c>vkSetEvent</c> / <c>vkGetEventStatus</c> /
    /// <c>vkResetEvent</c>, none of which the wrapper wraps today.
    /// </summary>
    None       = 0,

    /// <summary>
    /// <c>VK_EVENT_CREATE_DEVICE_ONLY_BIT</c> — the event is only ever
    /// signaled, waited on and reset from the device. The split-barrier
    /// case, and the default of <see cref="Device.CreateEvent"/>.
    /// </summary>
    DeviceOnly = 0x00000001,
}
