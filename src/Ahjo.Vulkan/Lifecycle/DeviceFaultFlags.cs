namespace Ahjo.Vulkan;

/// <summary>
/// Per-entry fault flags, mirroring <c>VkDeviceFaultFlagBitsKHR</c>.
/// </summary>
/// <remarks>
/// Always <see cref="None"/> on an EXT-sourced entry — EXT reports carry no
/// flags; the wrapper does not synthesize them. Unknown future bits pass
/// through numerically.
/// </remarks>
[Flags]
public enum DeviceFaultFlags : uint
{
    /// <summary>No flag set.</summary>
    None = 0,

    /// <summary>The fault resulted in a device-lost condition. No further
    /// entries are returned for the device after this one.</summary>
    DeviceLost = 0x1,

    /// <summary>The fault has associated memory-access address information
    /// (<c>faultAddressInfo</c>), surfaced in
    /// <see cref="DeviceFaultEntry.AddressInfos"/>.</summary>
    MemoryAddress = 0x2,

    /// <summary>The fault has an associated instruction address
    /// (<c>instructionAddressInfo</c>), surfaced in
    /// <see cref="DeviceFaultEntry.AddressInfos"/>.</summary>
    InstructionAddress = 0x4,

    /// <summary>The fault has associated vendor information
    /// (<c>vendorInfo</c>), surfaced in
    /// <see cref="DeviceFaultEntry.VendorInfos"/>.</summary>
    Vendor = 0x8,

    /// <summary>The fault was the result of a GPU timeout. Platform-specific
    /// extensions may make more information available.</summary>
    WatchdogTimeout = 0x10,

    /// <summary>Earlier faults occurred but information about them is no
    /// longer available — typically faults arriving faster than the
    /// application reads them back.</summary>
    Overflow = 0x20,
}
