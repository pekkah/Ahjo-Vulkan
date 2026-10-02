namespace Ahjo.Vulkan;

/// <summary>
/// One vendor-specific fault record, mirroring
/// <c>VkDeviceFaultVendorInfoKHR</c>.
/// </summary>
/// <param name="Description">The driver's description, decoded from its
/// bounded <c>char[256]</c> UTF-8 field (invalid sequences become
/// U+FFFD).</param>
/// <param name="VendorFaultCode">Vendor-specific fault code.</param>
/// <param name="VendorFaultData">Vendor-specific data associated with the
/// fault.</param>
public readonly record struct DeviceFaultVendorInfo(
    string Description,
    ulong  VendorFaultCode,
    ulong  VendorFaultData);
