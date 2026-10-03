namespace Ahjo.Vulkan;

/// <summary>
/// One fault within a <see cref="DeviceFaultReport"/>.
/// </summary>
/// <remarks>
/// <para><b>Mapping from the two native sources.</b></para>
/// <list type="table">
/// <listheader><term>Source</term><description>Entry fields</description></listheader>
/// <item><term>EXT (<c>VkDeviceFaultInfoEXT</c>; exactly one entry)</term>
/// <description><see cref="Description"/> from <c>description</c>;
/// <see cref="AddressInfos"/> and <see cref="VendorInfos"/> from the two
/// arrays; <see cref="Flags"/> = <see cref="DeviceFaultFlags.None"/>;
/// <see cref="GroupId"/> = 0.</description></item>
/// <item><term>KHR (one entry per drained <c>VkDeviceFaultInfoKHR</c>, in
/// order of occurrence)</term>
/// <description><see cref="Description"/>, <see cref="Flags"/> and
/// <see cref="GroupId"/> copied. <see cref="AddressInfos"/> =
/// <c>faultAddressInfo</c> if <see cref="DeviceFaultFlags.MemoryAddress"/> is
/// set, then <c>instructionAddressInfo</c> if
/// <see cref="DeviceFaultFlags.InstructionAddress"/> is set (0, 1 or 2
/// elements). <see cref="VendorInfos"/> = <c>vendorInfo</c> if
/// <see cref="DeviceFaultFlags.Vendor"/> is set.</description></item>
/// </list>
/// <para>KHR members whose flag bit is clear are omitted, whatever their bytes
/// contain. Flattening the two KHR address members into one array loses
/// nothing: <see cref="DeviceFaultAddressInfo.AddressType"/> says whether an
/// element is a memory access or an instruction pointer.</para>
/// <para>Publicly constructible, so a consumer can hand-build entries (for
/// example, to test a formatter).</para>
/// </remarks>
public sealed class DeviceFaultEntry
{
    /// <summary>The driver's description of the fault, decoded from its
    /// bounded <c>char[256]</c> UTF-8 field.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>KHR fault flags; always <see cref="DeviceFaultFlags.None"/>
    /// on an EXT-sourced entry.</summary>
    public DeviceFaultFlags Flags { get; init; }

    /// <summary>KHR group id: entries sharing one id stem from the same
    /// fault. Always 0 on an EXT-sourced entry.</summary>
    public ulong GroupId { get; init; }

    /// <summary>Faulting GPU virtual addresses. Never null.</summary>
    public DeviceFaultAddressInfo[] AddressInfos { get; init; } = [];

    /// <summary>Vendor-specific fault records. Never null.</summary>
    public DeviceFaultVendorInfo[] VendorInfos { get; init; } = [];
}
