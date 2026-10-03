namespace Ahjo.Vulkan;

/// <summary>
/// The result of <see cref="Device.TryGetDeviceFault(out DeviceFaultReport)"/>:
/// why a lost device was lost, as far as the driver can say.
/// </summary>
/// <remarks>
/// <para>Publicly constructible, so a consumer's formatter tests can hand-build
/// one.</para>
/// <para><b>Entries.</b> An EXT read always yields exactly one entry. A KHR
/// read yields zero or more, in order of occurrence.</para>
/// <para><b>No formatter.</b> No <c>ToString</c> override is provided; the
/// consumer owns presentation.</para>
/// </remarks>
public sealed class DeviceFaultReport
{
    /// <summary>The path the read took.</summary>
    public DeviceFaultApi Api { get; init; }

    /// <summary>The faults, one per entry. Never null.</summary>
    public DeviceFaultEntry[] Entries { get; init; } = [];

    /// <summary>
    /// The vendor's opaque binary crash dump. Never null; empty unless the
    /// driver produced one, which normally needs the
    /// <c>deviceFaultVendorBinary</c> feature.
    /// </summary>
    /// <remarks>Begins with the 56-byte
    /// <c>VkDeviceFaultVendorBinaryHeaderVersionOneKHR</c>; parsing the rest is
    /// for vendor tools.</remarks>
    public byte[] VendorBinary { get; init; } = [];

    /// <summary>
    /// <see langword="true"/> when the driver had more than was returned: an
    /// EXT or KHR debug-info <c>VK_INCOMPLETE</c>, a KHR drain cap reached
    /// while more entries were queued, or a KHR drain round that failed after
    /// some entries had already been drained.
    /// </summary>
    public bool IsIncomplete { get; init; }
}
