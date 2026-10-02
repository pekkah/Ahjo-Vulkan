using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// The three device-fault entry points a <see cref="Device"/> resolved, as one
/// value <see cref="DeviceFaultReader"/> dispatches on. Any of them may be
/// null (the owning extension was not enabled).
/// </summary>
internal readonly unsafe struct DeviceFaultEntryPoints
{
    public readonly delegate* unmanaged[Stdcall]<
        VkDevice_T*, VkDeviceFaultCountsEXT*, VkDeviceFaultInfoEXT*, VkResult> Ext;

    public readonly delegate* unmanaged[Stdcall]<
        VkDevice_T*, ulong, uint*, VkDeviceFaultInfoKHR*, VkResult> KhrReports;

    public readonly delegate* unmanaged[Stdcall]<
        VkDevice_T*, VkDeviceFaultDebugInfoKHR*, VkResult> KhrDebugInfo;

    public DeviceFaultEntryPoints(
        delegate* unmanaged[Stdcall]<VkDevice_T*, VkDeviceFaultCountsEXT*, VkDeviceFaultInfoEXT*, VkResult> ext,
        delegate* unmanaged[Stdcall]<VkDevice_T*, ulong, uint*, VkDeviceFaultInfoKHR*, VkResult> khrReports,
        delegate* unmanaged[Stdcall]<VkDevice_T*, VkDeviceFaultDebugInfoKHR*, VkResult> khrDebugInfo)
    {
        Ext          = ext;
        KhrReports   = khrReports;
        KhrDebugInfo = khrDebugInfo;
    }

    /// <summary>The path a read takes: KHR when both KHR pointers are present,
    /// otherwise EXT when its pointer is present, otherwise none.</summary>
    public DeviceFaultApi Api =>
        KhrReports != null && KhrDebugInfo != null ? DeviceFaultApi.Khr
        : Ext != null ? DeviceFaultApi.Ext
        : DeviceFaultApi.None;
}

/// <summary>
/// The <c>VK_EXT_device_fault</c> and <c>VK_KHR_device_fault</c> read
/// protocols, the selection between them, and the translation into
/// <see cref="DeviceFaultReport"/>.
/// </summary>
/// <remarks>
/// <para>Takes function pointers rather than a <see cref="Device"/> so every
/// protocol is testable against fake <c>[UnmanagedCallersOnly]</c>
/// functions — a real device loss cannot be produced portably.</para>
/// <para>Allocates by design (arrays, strings, the report), once, after device
/// loss — off every hot path.</para>
/// <para><see cref="TryRead"/> must only be reached through
/// <see cref="Device.TryGetDeviceFault(TimeSpan, out DeviceFaultReport)"/>'s
/// <see cref="Device.IsLost"/> gate: the EXT info and KHR debug-info commands
/// are legal only on a lost device (VUID-vkGetDeviceFaultInfoEXT-device-07336,
/// VUID-vkGetDeviceFaultDebugInfoKHR-device-12383). <see cref="ReadKhrEntries"/>
/// alone is legal on a healthy device (its command has no lost-state valid
/// usage), which the hardware test relies on.</para>
/// <para>Never throws for a driver result and never uses
/// <c>ThrowIfFailed</c> / <c>ThrowIfErrored</c>: results are branched on by
/// sign, and failures go to <see cref="AhjoDiagnostics.Sink"/>. A forensic
/// read on a crash path must not raise a second exception over the one the
/// caller is handling.</para>
/// </remarks>
internal static unsafe class DeviceFaultReader
{
    /// <summary>Upper bound on KHR entries one read requests. Entries past it
    /// are never requested, so they stay queued in the driver.</summary>
    internal const int MaxKhrEntries = 1024;

    /// <summary>Upper bound on KHR count/fill rounds one read makes.</summary>
    internal const int MaxKhrRounds = 8;

    // VK_MAX_DESCRIPTION_SIZE: every device-fault description field is char[256].
    private const int DescriptionSize = 256;

    internal static bool TryRead(
        in DeviceFaultEntryPoints eps,
        VkDevice_T*               device,
        ulong                     timeoutNs,
        [NotNullWhen(true)] out DeviceFaultReport? report)
    {
        report = null;
        switch (eps.Api)
        {
            case DeviceFaultApi.Ext:
            {
                VkResult r = ReadExt(eps.Ext, device, out DeviceFaultReport? extReport);
                if ((int)r < 0)
                {
                    AhjoDiagnostics.Write(DiagnosticSeverity.Warning, "Device",
                        $"Device.TryGetDeviceFault: vkGetDeviceFaultInfoEXT returned {r}; no device-fault report is available.");
                    return false;
                }
                report = extReport;
                return report is not null;
            }

            case DeviceFaultApi.Khr:
            {
                VkResult r = ReadKhrEntries(eps.KhrReports, device, timeoutNs,
                    out DeviceFaultEntry[] entries, out bool entriesIncomplete);
                if ((int)r < 0)
                {
                    if (entries.Length == 0)
                    {
                        AhjoDiagnostics.Write(DiagnosticSeverity.Warning, "Device",
                            $"Device.TryGetDeviceFault: vkGetDeviceFaultReportsKHR returned {r}; no device-fault report is available.");
                        return false;
                    }

                    // The entries are already drained from the driver queue;
                    // returning false would destroy the only copy.
                    AhjoDiagnostics.Write(DiagnosticSeverity.Warning, "Device",
                        $"Device.TryGetDeviceFault: vkGetDeviceFaultReportsKHR returned {r} after {entries.Length} entries were drained; returning a partial report.");
                    entriesIncomplete = true;
                }

                VkResult d = ReadKhrDebugInfo(eps.KhrDebugInfo, device, out byte[] binary, out bool binaryIncomplete);
                if ((int)d < 0)
                {
                    AhjoDiagnostics.Write(DiagnosticSeverity.Warning, "Device",
                        $"Device.TryGetDeviceFault: vkGetDeviceFaultDebugInfoKHR returned {d}; the report carries no vendor binary.");
                }

                report = new DeviceFaultReport
                {
                    Api          = DeviceFaultApi.Khr,
                    Entries      = entries,
                    VendorBinary = binary,
                    IsIncomplete = entriesIncomplete || binaryIncomplete,
                };
                return true;
            }

            default:
                // Unreachable from Device, which checks Api first.
                return false;
        }
    }

    /// <summary>
    /// <c>vkGetDeviceFaultInfoEXT</c> count/fill. Always makes the fill call
    /// (the description arrives only there), never retries (counts are
    /// identical across calls by spec). A negative result returns it with
    /// <paramref name="report"/> null.
    /// </summary>
    internal static VkResult ReadExt(
        delegate* unmanaged[Stdcall]<VkDevice_T*, VkDeviceFaultCountsEXT*, VkDeviceFaultInfoEXT*, VkResult> fn,
        VkDevice_T*            device,
        out DeviceFaultReport? report)
    {
        report = null;

        var counts = new VkDeviceFaultCountsEXT
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_FAULT_COUNTS_EXT,
            pNext = null,
        };
        VkResult r = fn(device, &counts, null);
        if ((int)r < 0) return r;

        var addresses = new VkDeviceFaultAddressInfoKHR[counts.addressInfoCount];
        var vendors   = new VkDeviceFaultVendorInfoKHR[counts.vendorInfoCount];
        // Clamp to what a managed array can hold, and write the clamped value
        // back: VUID-vkGetDeviceFaultInfoEXT-pFaultCounts-07339 ties the
        // pointer to the size passed.
        ulong binarySize = Math.Min(counts.vendorBinarySize, (ulong)Array.MaxLength);
        var   binary     = new byte[(int)binarySize];
        counts.vendorBinarySize = binarySize;

        var info = new VkDeviceFaultInfoEXT
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_FAULT_INFO_EXT,
            pNext = null,
        };
        // `fixed` over an empty array yields null, which is exactly the
        // pointer a zero count wants.
        fixed (VkDeviceFaultAddressInfoKHR* pAddresses = addresses)
        fixed (VkDeviceFaultVendorInfoKHR*  pVendors   = vendors)
        fixed (byte*                        pBinary    = binary)
        {
            info.pAddressInfos     = pAddresses;
            info.pVendorInfos      = pVendors;
            info.pVendorBinaryData = pBinary;
            r = fn(device, &counts, &info);
        }
        if ((int)r < 0) return r;

        int addressCount = (int)Math.Min(counts.addressInfoCount, (uint)addresses.Length);
        int vendorCount  = (int)Math.Min(counts.vendorInfoCount, (uint)vendors.Length);
        int binaryCount  = (int)Math.Min(counts.vendorBinarySize, (ulong)binary.Length);

        var addressInfos = new DeviceFaultAddressInfo[addressCount];
        for (int i = 0; i < addressCount; i++)
            addressInfos[i] = ToManaged(in addresses[i]);

        var vendorInfos = new DeviceFaultVendorInfo[vendorCount];
        for (int i = 0; i < vendorCount; i++)
            vendorInfos[i] = ToManaged(ref vendors[i]);

        report = new DeviceFaultReport
        {
            Api     = DeviceFaultApi.Ext,
            Entries =
            [
                new DeviceFaultEntry
                {
                    Description  = Utf8.FromBounded(
                        MemoryMarshal.CreateReadOnlySpan(ref info.description.e0, DescriptionSize)),
                    Flags        = DeviceFaultFlags.None,
                    GroupId      = 0,
                    AddressInfos = addressInfos,
                    VendorInfos  = vendorInfos,
                },
            ],
            VendorBinary = binaryCount == binary.Length ? binary : binary.AsSpan(0, binaryCount).ToArray(),
            IsIncomplete = r == VkResult.VK_INCOMPLETE,
        };
        return r;
    }

    /// <summary>
    /// <c>vkGetDeviceFaultReportsKHR</c> bounded drain. The first count call
    /// passes <paramref name="timeoutNs"/>; every other call passes 0. Stops on
    /// <c>VK_TIMEOUT</c>, a zero count, a non-<c>VK_INCOMPLETE</c> fill, or a
    /// cap (<see cref="MaxKhrEntries"/>, <see cref="MaxKhrRounds"/>). A
    /// negative result in any round returns it; <paramref name="entries"/>
    /// then holds what was already drained and is never null.
    /// </summary>
    internal static VkResult ReadKhrEntries(
        delegate* unmanaged[Stdcall]<VkDevice_T*, ulong, uint*, VkDeviceFaultInfoKHR*, VkResult> fn,
        VkDevice_T*            device,
        ulong                  timeoutNs,
        out DeviceFaultEntry[] entries,
        out bool               incomplete)
    {
        incomplete = false;
        var      collected = new List<DeviceFaultEntry>();
        VkResult last      = VkResult.VK_SUCCESS;

        for (int round = 0; round < MaxKhrRounds; round++)
        {
            uint     count = 0;
            VkResult r     = fn(device, round == 0 ? timeoutNs : 0, &count, null);
            if ((int)r < 0)
            {
                entries = collected.ToArray();
                return r;
            }
            last = r;
            if (r == VkResult.VK_TIMEOUT || count == 0) break;

            uint requested = Math.Min(count, (uint)(MaxKhrEntries - collected.Count));
            var  buffer    = new VkDeviceFaultInfoKHR[requested];
            // A `new T[n]` does not run the generated constructor, and the
            // layer checks sType/pNext on every element.
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i].sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_FAULT_INFO_KHR;
                buffer[i].pNext = null;
            }

            uint n = requested;
            fixed (VkDeviceFaultInfoKHR* p = buffer)
                r = fn(device, 0, &n, p);
            if ((int)r < 0)
            {
                entries = collected.ToArray();
                return r;
            }
            last = r;

            uint written = Math.Min(n, requested);
            for (int i = 0; i < (int)written; i++)
                collected.Add(ToManaged(ref buffer[i]));

            if (r != VkResult.VK_INCOMPLETE) break;
            if (collected.Count >= MaxKhrEntries || round + 1 >= MaxKhrRounds)
            {
                // A cap was reached while the driver still reports more; the
                // remainder stays queued, never requested.
                incomplete = true;
                break;
            }
        }

        entries = collected.ToArray();
        return last;
    }

    /// <summary>
    /// <c>vkGetDeviceFaultDebugInfoKHR</c> size/fill. A zero size returns
    /// <c>VK_SUCCESS</c> with no fill call. A negative result returns it with
    /// <paramref name="binary"/> empty.
    /// </summary>
    internal static VkResult ReadKhrDebugInfo(
        delegate* unmanaged[Stdcall]<VkDevice_T*, VkDeviceFaultDebugInfoKHR*, VkResult> fn,
        VkDevice_T* device,
        out byte[]  binary,
        out bool    incomplete)
    {
        binary     = [];
        incomplete = false;

        var info = new VkDeviceFaultDebugInfoKHR
        {
            sType             = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_FAULT_DEBUG_INFO_KHR,
            pNext             = null,
            vendorBinarySize  = 0,
            pVendorBinaryData = null,
        };
        VkResult r = fn(device, &info);
        if ((int)r < 0) return r;
        if (info.vendorBinarySize == 0) return VkResult.VK_SUCCESS;

        // Clamp to what a managed array can hold, and write the clamped value
        // back so the size passed matches the buffer.
        uint size   = Math.Min(info.vendorBinarySize, (uint)Array.MaxLength);
        var  buffer = new byte[size];
        info.vendorBinarySize = size;

        fixed (byte* p = buffer)
        {
            info.pVendorBinaryData = p;
            r = fn(device, &info);
        }
        if ((int)r < 0) return r;

        int written = (int)Math.Min(info.vendorBinarySize, size);
        binary     = written == buffer.Length ? buffer : buffer.AsSpan(0, written).ToArray();
        incomplete = r == VkResult.VK_INCOMPLETE;
        return r;
    }

    private static DeviceFaultEntry ToManaged(ref VkDeviceFaultInfoKHR e)
    {
        var flags = (DeviceFaultFlags)e.flags;

        // Flags decide, not field contents: a member whose bit is clear is
        // ignored whatever its bytes contain.
        bool hasFault       = (flags & DeviceFaultFlags.MemoryAddress) != 0;
        bool hasInstruction = (flags & DeviceFaultFlags.InstructionAddress) != 0;
        var  addressInfos   = new DeviceFaultAddressInfo[(hasFault ? 1 : 0) + (hasInstruction ? 1 : 0)];
        int  a              = 0;
        if (hasFault)       addressInfos[a++] = ToManaged(in e.faultAddressInfo);
        if (hasInstruction) addressInfos[a++] = ToManaged(in e.instructionAddressInfo);

        DeviceFaultVendorInfo[] vendorInfos = (flags & DeviceFaultFlags.Vendor) != 0
            ? [ToManaged(ref e.vendorInfo)]
            : [];

        return new DeviceFaultEntry
        {
            Description  = Utf8.FromBounded(
                MemoryMarshal.CreateReadOnlySpan(ref e.description.e0, DescriptionSize)),
            Flags        = flags,
            GroupId      = e.groupId,
            AddressInfos = addressInfos,
            VendorInfos  = vendorInfos,
        };
    }

    private static DeviceFaultAddressInfo ToManaged(in VkDeviceFaultAddressInfoKHR a) =>
        new((DeviceFaultAddressType)a.addressType, a.reportedAddress, a.addressPrecision);

    private static DeviceFaultVendorInfo ToManaged(ref VkDeviceFaultVendorInfoKHR v) =>
        new(Utf8.FromBounded(MemoryMarshal.CreateReadOnlySpan(ref v.description.e0, DescriptionSize)),
            v.vendorFaultCode,
            v.vendorFaultData);
}
