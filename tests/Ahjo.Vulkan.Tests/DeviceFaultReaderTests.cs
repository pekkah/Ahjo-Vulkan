using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Driverless coverage of <c>Internal/DeviceFaultReader</c> (#242): the EXT
/// count/fill protocol, the KHR drain loop and debug-info protocol, the
/// flag-driven translation, and <c>TryRead</c>'s selection and failure
/// isolation — all against fake <c>[UnmanagedCallersOnly]</c> entry points.
/// </summary>
/// <remarks>
/// <para>No <c>TestGate</c>: nothing here reaches a driver. The device pointer
/// is <c>(VkDevice_T*)0x1</c> and is never dereferenced.</para>
/// <para>Scenario and recorder state lives in static fields, reset by an
/// <c>Arrange…</c> helper at the start of every test. That is safe because the
/// suite runs serially (<c>xunit.runner.json</c>:
/// <c>parallelizeTestCollections: false</c>, <c>maxParallelThreads: 1</c>).</para>
/// <para>The fakes never assert (an exception inside an unmanaged callback is
/// a process crash, not a test failure): they record, and the test body
/// asserts. Any exception a fake hits is caught, stored in
/// <c>s_fakeException</c>, and turned into <c>VK_ERROR_UNKNOWN</c>. No fake
/// ever writes more elements or bytes than the caller passed; an override
/// changes only the count <i>reported back</i>.</para>
/// <para><b>Not unit-tested, by design:</b> the <c>Array.MaxLength</c> clamps
/// on the EXT and KHR vendor-binary sizes. Exercising them would genuinely
/// allocate more than 2 GB; they are covered by review of the reader.</para>
/// </remarks>
public sealed unsafe class DeviceFaultReaderTests
{
    private static readonly VkDevice_T* FakeDevice = (VkDevice_T*)0x1;

    private static Exception? s_fakeException;

    // =====================================================================
    // Fake: vkGetDeviceFaultInfoEXT
    // =====================================================================

    // Scenario.
    private static uint     s_extAddrAvail;
    private static uint     s_extVendorAvail;
    private static ulong    s_extBinaryAvail;
    private static VkResult s_extFirstResult;
    private static VkResult s_extSecondResult;
    private static uint?    s_extReportedAddrOverride;
    private static uint?    s_extReportedVendorOverride;
    private static ulong?   s_extReportedBinaryOverride;
    private static byte[]   s_extDescription = [];
    private static byte[]?  s_extVendorDescription;

    // Recorders.
    private static int                     s_extCalls;
    private static List<VkStructureType>   s_extCountsSTypes = [];
    private static List<nint>              s_extCountsPNexts = [];
    private static VkStructureType         s_extInfoSType;
    private static nint                    s_extInfoPNext;
    private static uint                    s_extFillAddrPassed;
    private static uint                    s_extFillVendorPassed;
    private static ulong                   s_extFillBinaryPassed;
    private static bool                    s_extFillAddrPtrNull;
    private static bool                    s_extFillVendorPtrNull;
    private static bool                    s_extFillBinaryPtrNull;

    private static void ArrangeExt(
        uint     addresses     = 0,
        uint     vendors       = 0,
        ulong    binary        = 0,
        VkResult first         = VkResult.VK_SUCCESS,
        VkResult second        = VkResult.VK_SUCCESS,
        string   description   = "ext fault")
    {
        s_fakeException             = null;
        s_extAddrAvail              = addresses;
        s_extVendorAvail            = vendors;
        s_extBinaryAvail            = binary;
        s_extFirstResult            = first;
        s_extSecondResult           = second;
        s_extReportedAddrOverride   = null;
        s_extReportedVendorOverride = null;
        s_extReportedBinaryOverride = null;
        s_extDescription            = System.Text.Encoding.UTF8.GetBytes(description);
        s_extVendorDescription      = null;

        s_extCalls             = 0;
        s_extCountsSTypes      = [];
        s_extCountsPNexts      = [];
        s_extInfoSType         = 0;
        s_extInfoPNext         = 0;
        s_extFillAddrPassed    = uint.MaxValue;
        s_extFillVendorPassed  = uint.MaxValue;
        s_extFillBinaryPassed  = ulong.MaxValue;
        s_extFillAddrPtrNull   = false;
        s_extFillVendorPtrNull = false;
        s_extFillBinaryPtrNull = false;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static VkResult FakeExt(VkDevice_T* device, VkDeviceFaultCountsEXT* counts, VkDeviceFaultInfoEXT* info)
    {
        try
        {
            s_extCalls++;
            s_extCountsSTypes.Add(counts->sType);
            s_extCountsPNexts.Add((nint)counts->pNext);

            if (info == null)
            {
                if ((int)s_extFirstResult < 0) return s_extFirstResult;
                counts->addressInfoCount = s_extAddrAvail;
                counts->vendorInfoCount  = s_extVendorAvail;
                counts->vendorBinarySize = s_extBinaryAvail;
                return s_extFirstResult;
            }

            s_extInfoSType         = info->sType;
            s_extInfoPNext         = (nint)info->pNext;
            s_extFillAddrPassed    = counts->addressInfoCount;
            s_extFillVendorPassed  = counts->vendorInfoCount;
            s_extFillBinaryPassed  = counts->vendorBinarySize;
            s_extFillAddrPtrNull   = info->pAddressInfos == null;
            s_extFillVendorPtrNull = info->pVendorInfos == null;
            s_extFillBinaryPtrNull = info->pVendorBinaryData == null;

            if ((int)s_extSecondResult < 0) return s_extSecondResult;

            WriteUtf8(MemoryMarshal.CreateSpan(ref info->description.e0, 256), s_extDescription);

            uint na = info->pAddressInfos == null ? 0 : Math.Min(counts->addressInfoCount, s_extAddrAvail);
            for (uint i = 0; i < na; i++)
            {
                info->pAddressInfos[i] = new VkDeviceFaultAddressInfoKHR
                {
                    addressType      = (VkDeviceFaultAddressTypeKHR)(i % 7),
                    reportedAddress  = 0x1000_0000UL + i * 0x100UL,
                    addressPrecision = 64,
                };
            }

            uint nv = info->pVendorInfos == null ? 0 : Math.Min(counts->vendorInfoCount, s_extVendorAvail);
            for (uint i = 0; i < nv; i++)
            {
                ref VkDeviceFaultVendorInfoKHR v = ref info->pVendorInfos[i];
                WriteUtf8(MemoryMarshal.CreateSpan(ref v.description.e0, 256),
                    s_extVendorDescription ?? System.Text.Encoding.UTF8.GetBytes("vendor-" + i));
                v.vendorFaultCode = 0xC0DEUL + i;
                v.vendorFaultData = 0xDA7AUL + i;
            }

            ulong nb = info->pVendorBinaryData == null ? 0 : Math.Min(counts->vendorBinarySize, s_extBinaryAvail);
            byte* bin = (byte*)info->pVendorBinaryData;
            for (ulong j = 0; j < nb; j++)
                bin[j] = (byte)j;

            counts->addressInfoCount = s_extReportedAddrOverride ?? na;
            counts->vendorInfoCount  = s_extReportedVendorOverride ?? nv;
            counts->vendorBinarySize = s_extReportedBinaryOverride ?? nb;
            return s_extSecondResult;
        }
        catch (Exception ex)
        {
            s_fakeException = ex;
            return VkResult.VK_ERROR_UNKNOWN;
        }
    }

    // =====================================================================
    // Fake: vkGetDeviceFaultReportsKHR
    // =====================================================================

    /// <summary>Per-call override. <c>Result</c> replaces the fake's natural
    /// result (a negative one returns before anything is written);
    /// <c>WriteCap</c> limits how many elements a fill call writes.</summary>
    private readonly record struct ReportsStep(VkResult? Result = null, int? WriteCap = null);

    // Scenario.
    private static Queue<VkDeviceFaultInfoKHR>  s_queue = new();
    private static Dictionary<int, ReportsStep> s_reportsScript = [];
    private static bool                         s_reportsInfinite;
    private static VkDeviceFaultInfoKHR         s_reportsTemplate;

    // Recorders.
    private static int                                    s_reportsCalls;
    private static int                                    s_reportsFillCalls;
    private static List<ulong>                            s_reportsTimeouts = [];
    private static List<uint>                             s_reportsRequested = [];
    private static List<(VkStructureType SType, nint PNext)> s_reportsElements = [];

    private static void ArrangeReports(params VkDeviceFaultInfoKHR[] queued)
    {
        s_fakeException    = null;
        s_queue            = new Queue<VkDeviceFaultInfoKHR>(queued);
        s_reportsScript    = [];
        s_reportsInfinite  = false;
        s_reportsTemplate  = default;
        s_reportsCalls     = 0;
        s_reportsFillCalls = 0;
        s_reportsTimeouts  = [];
        s_reportsRequested = [];
        s_reportsElements  = [];
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static VkResult FakeReports(VkDevice_T* device, ulong timeout, uint* pCount, VkDeviceFaultInfoKHR* pInfo)
    {
        try
        {
            int call = s_reportsCalls++;
            s_reportsTimeouts.Add(timeout);
            bool        scripted = s_reportsScript.TryGetValue(call, out ReportsStep step);
            VkResult?   forced   = scripted ? step.Result : null;

            if (pInfo == null)
            {
                if (forced is { } e && (int)e < 0) return e;
                if (s_reportsInfinite)
                {
                    *pCount = 1;
                    return forced ?? VkResult.VK_SUCCESS;
                }
                if (s_queue.Count == 0)
                {
                    *pCount = 0;
                    return forced ?? VkResult.VK_TIMEOUT;
                }
                *pCount = (uint)s_queue.Count;
                return forced ?? VkResult.VK_SUCCESS;
            }

            s_reportsFillCalls++;
            uint requested = *pCount;
            s_reportsRequested.Add(requested);
            for (uint i = 0; i < requested; i++)
                s_reportsElements.Add((pInfo[i].sType, (nint)pInfo[i].pNext));

            if (forced is { } fe && (int)fe < 0) return fe;

            uint available = s_reportsInfinite ? 1u : (uint)s_queue.Count;
            uint n         = Math.Min(requested, available);
            if (scripted && step.WriteCap is { } cap) n = Math.Min(n, (uint)cap);

            for (uint i = 0; i < n; i++)
            {
                // Dequeue what is written: the destructive semantics.
                VkDeviceFaultInfoKHR src = s_reportsInfinite ? s_reportsTemplate : s_queue.Dequeue();
                src.sType = pInfo[i].sType; // input fields stay the caller's
                src.pNext = pInfo[i].pNext;
                pInfo[i]  = src;
            }
            *pCount = n;

            if (forced is { } ok) return ok;
            return s_reportsInfinite || s_queue.Count > 0 ? VkResult.VK_INCOMPLETE : VkResult.VK_SUCCESS;
        }
        catch (Exception ex)
        {
            s_fakeException = ex;
            return VkResult.VK_ERROR_UNKNOWN;
        }
    }

    // =====================================================================
    // Fake: vkGetDeviceFaultDebugInfoKHR
    // =====================================================================

    // Scenario.
    private static uint     s_debugLength;
    private static VkResult s_debugFirstResult;
    private static VkResult s_debugSecondResult;

    // Recorders.
    private static int                   s_debugCalls;
    private static List<VkStructureType> s_debugSTypes = [];
    private static List<nint>            s_debugPNexts = [];
    private static uint                  s_debugSizeOnQuery;
    private static uint                  s_debugSizePassedOnFill;

    private static void ArrangeDebug(
        uint     length = 0,
        VkResult first  = VkResult.VK_SUCCESS,
        VkResult second = VkResult.VK_SUCCESS)
    {
        s_fakeException         = null;
        s_debugLength           = length;
        s_debugFirstResult      = first;
        s_debugSecondResult     = second;
        s_debugCalls            = 0;
        s_debugSTypes           = [];
        s_debugPNexts           = [];
        s_debugSizeOnQuery      = uint.MaxValue;
        s_debugSizePassedOnFill = uint.MaxValue;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static VkResult FakeDebugInfo(VkDevice_T* device, VkDeviceFaultDebugInfoKHR* info)
    {
        try
        {
            int call = s_debugCalls++;
            s_debugSTypes.Add(info->sType);
            s_debugPNexts.Add((nint)info->pNext);
            VkResult r = call == 0 ? s_debugFirstResult : s_debugSecondResult;

            if (info->pVendorBinaryData == null)
            {
                s_debugSizeOnQuery = info->vendorBinarySize;
                if ((int)r < 0) return r;
                info->vendorBinarySize = s_debugLength;
                return r;
            }

            s_debugSizePassedOnFill = info->vendorBinarySize;
            if ((int)r < 0) return r;
            uint n   = Math.Min(info->vendorBinarySize, s_debugLength);
            byte* p  = (byte*)info->pVendorBinaryData;
            for (uint j = 0; j < n; j++)
                p[j] = (byte)j;
            info->vendorBinarySize = n;
            return r;
        }
        catch (Exception ex)
        {
            s_fakeException = ex;
            return VkResult.VK_ERROR_UNKNOWN;
        }
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static void WriteUtf8(Span<sbyte> destination, ReadOnlySpan<byte> utf8)
    {
        destination.Clear();
        int n = Math.Min(destination.Length, utf8.Length);
        MemoryMarshal.Cast<byte, sbyte>(utf8[..n]).CopyTo(destination);
    }

    private static VkDeviceFaultInfoKHR MakeKhr(
        DeviceFaultFlags flags,
        ulong            groupId     = 0,
        string           description = "khr fault")
    {
        var e = new VkDeviceFaultInfoKHR
        {
            flags   = (uint)flags,
            groupId = groupId,
            faultAddressInfo = new VkDeviceFaultAddressInfoKHR
            {
                addressType      = VkDeviceFaultAddressTypeKHR.VK_DEVICE_FAULT_ADDRESS_TYPE_READ_INVALID_KHR,
                reportedAddress  = 0xAAAA_0000UL + groupId,
                addressPrecision = 128,
            },
            instructionAddressInfo = new VkDeviceFaultAddressInfoKHR
            {
                addressType      = VkDeviceFaultAddressTypeKHR.VK_DEVICE_FAULT_ADDRESS_TYPE_INSTRUCTION_POINTER_FAULT_KHR,
                reportedAddress  = 0xBBBB_0000UL + groupId,
                addressPrecision = 4,
            },
        };
        // Overwritten by the fake with the caller's values; deliberately junk
        // here so a reader that relied on the fake's sType would be caught.
        e.sType = 0;
        WriteUtf8(MemoryMarshal.CreateSpan(ref e.description.e0, 256),
            System.Text.Encoding.UTF8.GetBytes(description));
        WriteUtf8(MemoryMarshal.CreateSpan(ref e.vendorInfo.description.e0, 256),
            System.Text.Encoding.UTF8.GetBytes("khr-vendor-" + groupId));
        e.vendorInfo.vendorFaultCode = 0x7000UL + groupId;
        e.vendorInfo.vendorFaultData = 0x8000UL + groupId;
        return e;
    }

    private static DeviceFaultEntryPoints ExtOnly() => new(&FakeExt, null, null);

    private static DeviceFaultEntryPoints KhrOnly() => new(null, &FakeReports, &FakeDebugInfo);

    private static DeviceFaultEntryPoints Both() => new(&FakeExt, &FakeReports, &FakeDebugInfo);

    private static List<(DiagnosticSeverity Severity, string Message)> InstallDeviceSink(out DiagnosticSink original)
    {
        var captured = new List<(DiagnosticSeverity, string)>();
        original = AhjoDiagnostics.Sink;
        AhjoDiagnostics.Sink = (severity, source, message) =>
        {
            if (source == "Device") captured.Add((severity, message));
        };
        return captured;
    }

    private static void AssertNoFakeException() =>
        Assert.True(s_fakeException is null, "A fake threw: " + s_fakeException);

    // =====================================================================
    // EXT (10)
    // =====================================================================

    [Fact]
    public void Ext_ZeroCounts_StillMakesFillCall_AndPassesNullPointers()
    {
        ArrangeExt(description: "only a description");

        VkResult r = DeviceFaultReader.ReadExt(&FakeExt, FakeDevice, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_SUCCESS, r);
        Assert.Equal(2, s_extCalls);
        Assert.True(s_extFillAddrPtrNull);
        Assert.True(s_extFillVendorPtrNull);
        Assert.True(s_extFillBinaryPtrNull);
        Assert.NotNull(report);
        Assert.Equal("only a description", Assert.Single(report.Entries).Description);
    }

    [Fact]
    public void Ext_Populated_MapsToExactlyOneEntry()
    {
        ArrangeExt(addresses: 3, vendors: 2, binary: 10, description: "page fault");

        VkResult r = DeviceFaultReader.ReadExt(&FakeExt, FakeDevice, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_SUCCESS, r);
        Assert.NotNull(report);
        Assert.Equal(DeviceFaultApi.Ext, report.Api);
        Assert.False(report.IsIncomplete);

        DeviceFaultEntry entry = Assert.Single(report.Entries);
        Assert.Equal("page fault", entry.Description);
        Assert.Equal(DeviceFaultFlags.None, entry.Flags);
        Assert.Equal(0UL, entry.GroupId);

        Assert.Equal(3, entry.AddressInfos.Length);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal((DeviceFaultAddressType)(i % 7), entry.AddressInfos[i].AddressType);
            Assert.Equal(0x1000_0000UL + (ulong)i * 0x100UL, entry.AddressInfos[i].ReportedAddress);
            Assert.Equal(64UL, entry.AddressInfos[i].AddressPrecision);
        }

        Assert.Equal(2, entry.VendorInfos.Length);
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal("vendor-" + i, entry.VendorInfos[i].Description);
            Assert.Equal(0xC0DEUL + (ulong)i, entry.VendorInfos[i].VendorFaultCode);
            Assert.Equal(0xDA7AUL + (ulong)i, entry.VendorInfos[i].VendorFaultData);
        }

        Assert.Equal(10, report.VendorBinary.Length);
        for (int j = 0; j < 10; j++)
            Assert.Equal((byte)j, report.VendorBinary[j]);
    }

    [Fact]
    public void Ext_SetsSTypeAndNullPNext_OnBothStructs()
    {
        ArrangeExt(addresses: 1, vendors: 1, binary: 4);

        DeviceFaultReader.ReadExt(&FakeExt, FakeDevice, out _);

        AssertNoFakeException();
        Assert.Equal(2, s_extCountsSTypes.Count);
        Assert.All(s_extCountsSTypes, s => Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_DEVICE_FAULT_COUNTS_EXT, s));
        Assert.All(s_extCountsPNexts, p => Assert.Equal(0, p));
        Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_DEVICE_FAULT_INFO_EXT, s_extInfoSType);
        Assert.Equal(0, s_extInfoPNext);
    }

    [Fact]
    public void Ext_Description_Unterminated256Bytes_IsBounded()
    {
        ArrangeExt(addresses: 1, description: new string('D', 256));

        DeviceFaultReader.ReadExt(&FakeExt, FakeDevice, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.NotNull(report);
        Assert.Equal(new string('D', 256), Assert.Single(report.Entries).Description);
    }

    [Fact]
    public void Ext_VendorDescription_MultiByteUtf8_RoundTrips()
    {
        ArrangeExt(vendors: 1);
        s_extVendorDescription = System.Text.Encoding.UTF8.GetBytes("Störung ✓ GPU");

        DeviceFaultReader.ReadExt(&FakeExt, FakeDevice, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.NotNull(report);
        DeviceFaultVendorInfo vendor = Assert.Single(Assert.Single(report.Entries).VendorInfos);
        Assert.Equal("Störung ✓ GPU", vendor.Description);
    }

    [Fact]
    public void Ext_Incomplete_KeepsPartialData_AndFlagsIt()
    {
        ArrangeExt(addresses: 2, vendors: 1, binary: 8, second: VkResult.VK_INCOMPLETE);

        VkResult r = DeviceFaultReader.ReadExt(&FakeExt, FakeDevice, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_INCOMPLETE, r);
        Assert.NotNull(report);
        Assert.True(report.IsIncomplete);
        DeviceFaultEntry entry = Assert.Single(report.Entries);
        Assert.Equal(2, entry.AddressInfos.Length);
        Assert.Single(entry.VendorInfos);
        Assert.Equal(8, report.VendorBinary.Length);
        Assert.Equal(2, s_extCalls); // no retry
    }

    [Fact]
    public void Ext_FirstCallError_ReturnsError_NoFillCall()
    {
        ArrangeExt(addresses: 1, first: VkResult.VK_ERROR_OUT_OF_HOST_MEMORY);

        VkResult r = DeviceFaultReader.ReadExt(&FakeExt, FakeDevice, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_ERROR_OUT_OF_HOST_MEMORY, r);
        Assert.Null(report);
        Assert.Equal(1, s_extCalls);
    }

    [Fact]
    public void Ext_SecondCallError_ReturnsError_NoReport()
    {
        ArrangeExt(addresses: 1, vendors: 1, second: VkResult.VK_ERROR_UNKNOWN);

        VkResult r = DeviceFaultReader.ReadExt(&FakeExt, FakeDevice, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_ERROR_UNKNOWN, r);
        Assert.Null(report);
        Assert.Equal(2, s_extCalls);
    }

    [Fact]
    public void Ext_ReturnedCountsLargerThanAllocated_AreClamped()
    {
        ArrangeExt(addresses: 2, vendors: 1, binary: 4);
        s_extReportedAddrOverride   = 100;
        s_extReportedVendorOverride = 50;
        s_extReportedBinaryOverride = 4096;

        VkResult r = DeviceFaultReader.ReadExt(&FakeExt, FakeDevice, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_SUCCESS, r);
        Assert.NotNull(report);
        DeviceFaultEntry entry = Assert.Single(report.Entries);
        Assert.Equal(2, entry.AddressInfos.Length);
        Assert.Single(entry.VendorInfos);
        Assert.Equal(4, report.VendorBinary.Length);
    }

    [Fact]
    public void Ext_PassesAllocatedSizes_OnFillCall()
    {
        ArrangeExt(addresses: 5, vendors: 3, binary: 77);

        DeviceFaultReader.ReadExt(&FakeExt, FakeDevice, out _);

        AssertNoFakeException();
        Assert.Equal(5u, s_extFillAddrPassed);
        Assert.Equal(3u, s_extFillVendorPassed);
        Assert.Equal(77UL, s_extFillBinaryPassed);
        Assert.False(s_extFillAddrPtrNull);
        Assert.False(s_extFillVendorPtrNull);
        Assert.False(s_extFillBinaryPtrNull);
    }

    // =====================================================================
    // KHR entries (10)
    // =====================================================================

    [Fact]
    public void Khr_TimeoutOnCount_NoFillCall_ZeroEntries()
    {
        ArrangeReports();

        VkResult r = DeviceFaultReader.ReadKhrEntries(&FakeReports, FakeDevice, 0,
            out DeviceFaultEntry[] entries, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_TIMEOUT, r);
        Assert.NotNull(entries);
        Assert.Empty(entries);
        Assert.False(incomplete);
        Assert.Equal(0, s_reportsFillCalls);
    }

    [Fact]
    public void Khr_FlagsSelectMembers()
    {
        ArrangeReports(
            MakeKhr(DeviceFaultFlags.MemoryAddress, groupId: 1),
            MakeKhr(DeviceFaultFlags.InstructionAddress, groupId: 2),
            MakeKhr(DeviceFaultFlags.MemoryAddress | DeviceFaultFlags.InstructionAddress | DeviceFaultFlags.Vendor, groupId: 3),
            MakeKhr(DeviceFaultFlags.DeviceLost, groupId: 4));

        VkResult r = DeviceFaultReader.ReadKhrEntries(&FakeReports, FakeDevice, 0,
            out DeviceFaultEntry[] entries, out _);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_SUCCESS, r);
        Assert.Equal(4, entries.Length);

        Assert.Equal(DeviceFaultAddressType.ReadInvalid, Assert.Single(entries[0].AddressInfos).AddressType);
        Assert.Empty(entries[0].VendorInfos);

        Assert.Equal(DeviceFaultAddressType.InstructionPointerFault, Assert.Single(entries[1].AddressInfos).AddressType);
        Assert.Empty(entries[1].VendorInfos);

        // Fault-then-instruction order.
        Assert.Equal(2, entries[2].AddressInfos.Length);
        Assert.Equal(DeviceFaultAddressType.ReadInvalid, entries[2].AddressInfos[0].AddressType);
        Assert.Equal(0xAAAA_0003UL, entries[2].AddressInfos[0].ReportedAddress);
        Assert.Equal(128UL, entries[2].AddressInfos[0].AddressPrecision);
        Assert.Equal(DeviceFaultAddressType.InstructionPointerFault, entries[2].AddressInfos[1].AddressType);
        Assert.Equal(0xBBBB_0003UL, entries[2].AddressInfos[1].ReportedAddress);
        DeviceFaultVendorInfo vendor = Assert.Single(entries[2].VendorInfos);
        Assert.Equal("khr-vendor-3", vendor.Description);
        Assert.Equal(0x7003UL, vendor.VendorFaultCode);
        Assert.Equal(0x8003UL, vendor.VendorFaultData);

        Assert.Empty(entries[3].AddressInfos);
        Assert.Empty(entries[3].VendorInfos);
        Assert.Equal(DeviceFaultFlags.DeviceLost, entries[3].Flags);

        for (int i = 0; i < 4; i++)
        {
            Assert.Equal((ulong)(i + 1), entries[i].GroupId);
            Assert.Equal("khr fault", entries[i].Description);
        }
    }

    [Fact]
    public void Khr_FlagsClear_GarbageInMembersIsIgnored()
    {
        // MakeKhr fills all three members with non-zero bytes.
        ArrangeReports(MakeKhr(DeviceFaultFlags.None, groupId: 9));

        DeviceFaultReader.ReadKhrEntries(&FakeReports, FakeDevice, 0, out DeviceFaultEntry[] entries, out _);

        AssertNoFakeException();
        DeviceFaultEntry entry = Assert.Single(entries);
        Assert.Equal(DeviceFaultFlags.None, entry.Flags);
        Assert.Empty(entry.AddressInfos);
        Assert.Empty(entry.VendorInfos);
    }

    [Fact]
    public void Khr_EveryElement_HasSTypeAndNullPNext()
    {
        ArrangeReports(
            MakeKhr(DeviceFaultFlags.None),
            MakeKhr(DeviceFaultFlags.None),
            MakeKhr(DeviceFaultFlags.None));

        DeviceFaultReader.ReadKhrEntries(&FakeReports, FakeDevice, 0, out _, out _);

        AssertNoFakeException();
        Assert.Equal(3, s_reportsElements.Count);
        Assert.All(s_reportsElements, e =>
        {
            Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_DEVICE_FAULT_INFO_KHR, e.SType);
            Assert.Equal(0, e.PNext);
        });
    }

    [Fact]
    public void Khr_CallerTimeoutOnFirstCountCall_ZeroOnAllOthers()
    {
        ArrangeReports(MakeKhr(DeviceFaultFlags.None), MakeKhr(DeviceFaultFlags.None));
        s_reportsScript[1] = new ReportsStep(WriteCap: 1); // force a second round

        DeviceFaultReader.ReadKhrEntries(&FakeReports, FakeDevice, 5_000_000, out DeviceFaultEntry[] entries, out _);

        AssertNoFakeException();
        Assert.Equal(2, entries.Length);
        Assert.True(s_reportsTimeouts.Count >= 4, "expected two count/fill rounds");
        Assert.Equal(5_000_000UL, s_reportsTimeouts[0]);
        for (int i = 1; i < s_reportsTimeouts.Count; i++)
            Assert.Equal(0UL, s_reportsTimeouts[i]);
    }

    [Fact]
    public void Khr_Incomplete_DrainsInSecondRound()
    {
        ArrangeReports(
            MakeKhr(DeviceFaultFlags.None, groupId: 0),
            MakeKhr(DeviceFaultFlags.None, groupId: 1),
            MakeKhr(DeviceFaultFlags.None, groupId: 2),
            MakeKhr(DeviceFaultFlags.None, groupId: 3),
            MakeKhr(DeviceFaultFlags.None, groupId: 4));
        s_reportsScript[1] = new ReportsStep(WriteCap: 3); // first fill: 3 of 5, VK_INCOMPLETE

        VkResult r = DeviceFaultReader.ReadKhrEntries(&FakeReports, FakeDevice, 0,
            out DeviceFaultEntry[] entries, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_SUCCESS, r);
        Assert.False(incomplete);
        Assert.Equal(5, entries.Length);
        for (int i = 0; i < 5; i++)
            Assert.Equal((ulong)i, entries[i].GroupId);
        Assert.Equal(2, s_reportsFillCalls);
        Assert.Empty(s_queue);
    }

    [Fact]
    public void Khr_EntryCap_StopsRequesting_AndFlagsIncomplete()
    {
        var queued = new VkDeviceFaultInfoKHR[1500];
        for (int i = 0; i < queued.Length; i++)
            queued[i] = MakeKhr(DeviceFaultFlags.None, groupId: (ulong)i);
        ArrangeReports(queued);

        DeviceFaultReader.ReadKhrEntries(&FakeReports, FakeDevice, 0,
            out DeviceFaultEntry[] entries, out bool incomplete);

        AssertNoFakeException();
        long totalRequested = 0;
        foreach (uint n in s_reportsRequested) totalRequested += n;
        Assert.Equal(DeviceFaultReader.MaxKhrEntries, totalRequested);
        Assert.Equal(DeviceFaultReader.MaxKhrEntries, entries.Length);
        Assert.Equal(476, s_queue.Count); // never requested, so still queued
        Assert.True(incomplete);
    }

    [Fact]
    public void Khr_RoundCap_StopsAfterMaxRounds()
    {
        ArrangeReports();
        s_reportsInfinite = true;
        s_reportsTemplate = MakeKhr(DeviceFaultFlags.None);

        DeviceFaultReader.ReadKhrEntries(&FakeReports, FakeDevice, 0,
            out DeviceFaultEntry[] entries, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(DeviceFaultReader.MaxKhrRounds, s_reportsFillCalls);
        Assert.Equal(DeviceFaultReader.MaxKhrRounds, entries.Length);
        Assert.True(incomplete);
    }

    [Fact]
    public void Khr_FirstCountError_ReturnsError_NoEntries()
    {
        ArrangeReports(MakeKhr(DeviceFaultFlags.None));
        s_reportsScript[0] = new ReportsStep(Result: VkResult.VK_ERROR_UNKNOWN);

        VkResult r = DeviceFaultReader.ReadKhrEntries(&FakeReports, FakeDevice, 0,
            out DeviceFaultEntry[] entries, out _);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_ERROR_UNKNOWN, r);
        Assert.NotNull(entries);
        Assert.Empty(entries);
        Assert.Equal(0, s_reportsFillCalls);
    }

    [Fact]
    public void Khr_SecondRoundError_KeepsFirstRoundEntries()
    {
        ArrangeReports(
            MakeKhr(DeviceFaultFlags.None, groupId: 0),
            MakeKhr(DeviceFaultFlags.None, groupId: 1),
            MakeKhr(DeviceFaultFlags.None, groupId: 2),
            MakeKhr(DeviceFaultFlags.None, groupId: 3),
            MakeKhr(DeviceFaultFlags.None, groupId: 4));
        s_reportsScript[1] = new ReportsStep(WriteCap: 3);
        s_reportsScript[2] = new ReportsStep(Result: VkResult.VK_ERROR_OUT_OF_HOST_MEMORY);

        VkResult r = DeviceFaultReader.ReadKhrEntries(&FakeReports, FakeDevice, 0,
            out DeviceFaultEntry[] entries, out _);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_ERROR_OUT_OF_HOST_MEMORY, r);
        Assert.Equal(3, entries.Length);
        for (int i = 0; i < 3; i++)
            Assert.Equal((ulong)i, entries[i].GroupId);
    }

    // =====================================================================
    // KHR debug info (4)
    // =====================================================================

    [Fact]
    public void KhrDebug_SizeThenFill_WritesBackSize_SetsSTypeNullPNext()
    {
        ArrangeDebug(length: 100);

        VkResult r = DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            out byte[] binary, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_SUCCESS, r);
        Assert.False(incomplete);
        Assert.Equal(2, s_debugCalls);
        Assert.Equal(0u, s_debugSizeOnQuery);
        Assert.Equal(100u, s_debugSizePassedOnFill);
        Assert.All(s_debugSTypes, s => Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_DEVICE_FAULT_DEBUG_INFO_KHR, s));
        Assert.All(s_debugPNexts, p => Assert.Equal(0, p));
        Assert.Equal(100, binary.Length);
        for (int j = 0; j < 100; j++)
            Assert.Equal((byte)j, binary[j]);
    }

    [Fact]
    public void KhrDebug_SizeZero_NoFillCall_EmptyBinary()
    {
        ArrangeDebug(length: 0);

        VkResult r = DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            out byte[] binary, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_SUCCESS, r);
        Assert.Equal(1, s_debugCalls);
        Assert.NotNull(binary);
        Assert.Empty(binary);
        Assert.False(incomplete);
    }

    [Fact]
    public void KhrDebug_Incomplete_KeepsBytes_AndFlagsIt()
    {
        ArrangeDebug(length: 50, second: VkResult.VK_INCOMPLETE);

        VkResult r = DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            out byte[] binary, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_INCOMPLETE, r);
        Assert.True(incomplete);
        Assert.Equal(50, binary.Length);
    }

    [Fact]
    public void KhrDebug_Error_ReturnsError_EmptyBinary()
    {
        ArrangeDebug(length: 64, second: VkResult.VK_ERROR_NOT_ENOUGH_SPACE_KHR);

        VkResult r = DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            out byte[] binary, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_ERROR_NOT_ENOUGH_SPACE_KHR, r);
        Assert.NotNull(binary);
        Assert.Empty(binary);
        Assert.False(incomplete);
    }

    // =====================================================================
    // TryRead composition (6)
    // =====================================================================

    [Fact]
    public void TryRead_BothSupplied_UsesKhrOnly()
    {
        ArrangeExt(addresses: 1);
        ArrangeReports(MakeKhr(DeviceFaultFlags.DeviceLost));
        ArrangeDebug();
        DeviceFaultEntryPoints eps = Both();

        Assert.Equal(DeviceFaultApi.Khr, eps.Api);
        bool ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.True(ok);
        Assert.Equal(DeviceFaultApi.Khr, report!.Api);
        Assert.Equal(0, s_extCalls);
        Assert.Single(report.Entries);
    }

    [Fact]
    public void TryRead_ExtOnly_ReturnsOneEntry_ApiExt()
    {
        ArrangeExt(addresses: 2, vendors: 1, binary: 3, description: "ext only");
        DeviceFaultEntryPoints eps = ExtOnly();

        Assert.Equal(DeviceFaultApi.Ext, eps.Api);
        bool ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.True(ok);
        Assert.Equal(DeviceFaultApi.Ext, report!.Api);
        DeviceFaultEntry entry = Assert.Single(report.Entries);
        Assert.Equal("ext only", entry.Description);
        Assert.Equal(2, entry.AddressInfos.Length);
        Assert.Equal(3, report.VendorBinary.Length);
    }

    [Fact]
    public void TryRead_Khr_CombinesEntriesAndBinary()
    {
        ArrangeReports(
            MakeKhr(DeviceFaultFlags.MemoryAddress, groupId: 7),
            MakeKhr(DeviceFaultFlags.DeviceLost, groupId: 7));
        ArrangeDebug(length: 64);
        DeviceFaultEntryPoints eps = KhrOnly();

        bool ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.True(ok);
        Assert.Equal(DeviceFaultApi.Khr, report!.Api);
        Assert.Equal(2, report.Entries.Length);
        Assert.Equal(64, report.VendorBinary.Length);
        Assert.False(report.IsIncomplete);
    }

    [Fact]
    public void TryRead_ErrorWithNothingCollected_ReturnsFalse_OneWarning()
    {
        // EXT: first call fails.
        ArrangeExt(first: VkResult.VK_ERROR_UNKNOWN);
        DeviceFaultEntryPoints ext = ExtOnly();
        var extWarnings = InstallDeviceSink(out DiagnosticSink original);
        bool extOk;
        DeviceFaultReport? extReport;
        try
        {
            extOk = DeviceFaultReader.TryRead(in ext, FakeDevice, 0, out extReport);
        }
        finally
        {
            AhjoDiagnostics.Sink = original;
        }

        AssertNoFakeException();
        Assert.False(extOk);
        Assert.Null(extReport);
        var extWarning = Assert.Single(extWarnings);
        Assert.Equal(DiagnosticSeverity.Warning, extWarning.Severity);
        Assert.Equal(
            $"Device.TryGetDeviceFault: vkGetDeviceFaultInfoEXT returned {VkResult.VK_ERROR_UNKNOWN}; no device-fault report is available.",
            extWarning.Message);

        // KHR: first count call fails.
        ArrangeReports(MakeKhr(DeviceFaultFlags.None));
        s_reportsScript[0] = new ReportsStep(Result: VkResult.VK_ERROR_OUT_OF_HOST_MEMORY);
        ArrangeDebug(length: 8);
        DeviceFaultEntryPoints khr = KhrOnly();
        var khrWarnings = InstallDeviceSink(out original);
        bool khrOk;
        DeviceFaultReport? khrReport;
        try
        {
            khrOk = DeviceFaultReader.TryRead(in khr, FakeDevice, 0, out khrReport);
        }
        finally
        {
            AhjoDiagnostics.Sink = original;
        }

        AssertNoFakeException();
        Assert.False(khrOk);
        Assert.Null(khrReport);
        var khrWarning = Assert.Single(khrWarnings);
        Assert.Equal(DiagnosticSeverity.Warning, khrWarning.Severity);
        Assert.Equal(
            $"Device.TryGetDeviceFault: vkGetDeviceFaultReportsKHR returned {VkResult.VK_ERROR_OUT_OF_HOST_MEMORY}; no device-fault report is available.",
            khrWarning.Message);
        Assert.Equal(0, s_debugCalls);
    }

    [Fact]
    public void TryRead_KhrPartialDrainError_ReturnsTrue_IncompleteAndWarning()
    {
        ArrangeReports(
            MakeKhr(DeviceFaultFlags.None, groupId: 0),
            MakeKhr(DeviceFaultFlags.None, groupId: 1),
            MakeKhr(DeviceFaultFlags.None, groupId: 2),
            MakeKhr(DeviceFaultFlags.None, groupId: 3),
            MakeKhr(DeviceFaultFlags.None, groupId: 4));
        s_reportsScript[1] = new ReportsStep(WriteCap: 3);
        s_reportsScript[2] = new ReportsStep(Result: VkResult.VK_ERROR_OUT_OF_HOST_MEMORY);
        ArrangeDebug();
        DeviceFaultEntryPoints eps = KhrOnly();

        var warnings = InstallDeviceSink(out DiagnosticSink original);
        bool ok;
        DeviceFaultReport? report;
        try
        {
            ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, out report);
        }
        finally
        {
            AhjoDiagnostics.Sink = original;
        }

        AssertNoFakeException();
        Assert.True(ok);
        Assert.True(report!.IsIncomplete);
        Assert.Equal(3, report.Entries.Length);
        var warning = Assert.Single(warnings);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Equal(
            $"Device.TryGetDeviceFault: vkGetDeviceFaultReportsKHR returned {VkResult.VK_ERROR_OUT_OF_HOST_MEMORY} after 3 entries were drained; returning a partial report.",
            warning.Message);
    }

    [Fact]
    public void TryRead_KhrDebugInfoError_KeepsEntries_EmptyBinary_Warning()
    {
        ArrangeReports(MakeKhr(DeviceFaultFlags.DeviceLost, groupId: 1));
        ArrangeDebug(length: 32, second: VkResult.VK_ERROR_NOT_ENOUGH_SPACE_KHR);
        DeviceFaultEntryPoints eps = KhrOnly();

        var warnings = InstallDeviceSink(out DiagnosticSink original);
        bool ok;
        DeviceFaultReport? report;
        try
        {
            ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, out report);
        }
        finally
        {
            AhjoDiagnostics.Sink = original;
        }

        AssertNoFakeException();
        Assert.True(ok);
        Assert.Single(report!.Entries);
        Assert.Empty(report.VendorBinary);
        Assert.False(report.IsIncomplete);
        var warning = Assert.Single(warnings);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Equal(
            $"Device.TryGetDeviceFault: vkGetDeviceFaultDebugInfoKHR returned {VkResult.VK_ERROR_NOT_ENOUGH_SPACE_KHR}; the report carries no vendor binary.",
            warning.Message);
    }
}
