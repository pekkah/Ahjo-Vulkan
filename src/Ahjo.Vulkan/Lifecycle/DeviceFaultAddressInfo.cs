namespace Ahjo.Vulkan;

/// <summary>
/// One faulting GPU virtual address, mirroring
/// <c>VkDeviceFaultAddressInfoKHR</c>.
/// </summary>
/// <param name="AddressType">What the address describes.</param>
/// <param name="ReportedAddress">The address the driver reported.</param>
/// <param name="AddressPrecision">The precision of
/// <paramref name="ReportedAddress"/>; a power of two. The faulting address
/// lies between <c>ReportedAddress &amp; ~(AddressPrecision - 1)</c> (lower
/// bound) and <c>ReportedAddress | (AddressPrecision - 1)</c> (upper bound),
/// inclusive.</param>
public readonly record struct DeviceFaultAddressInfo(
    DeviceFaultAddressType AddressType,
    ulong                  ReportedAddress,
    ulong                  AddressPrecision);
