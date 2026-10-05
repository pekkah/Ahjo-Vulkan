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
/// <para>The allocation caps (<c>MaxAddressInfos</c>, <c>MaxVendorInfos</c>,
/// <c>MaxVendorBinaryBytes</c>) are exercised directly: a fake reports a
/// garbage count or size above the cap, and the test checks that the cap is
/// what the fill call was passed and that the report is marked incomplete.
/// The binary-cap cases allocate the 64 MiB cap once each.</para>
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
    // When true (the default), a VK_SUCCESS-scripted fill that was passed less
    // than is available answers VK_INCOMPLETE, as a conforming driver must.
    private static bool     s_extConformingIncomplete;

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
        s_extConformingIncomplete   = true;

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
            bool short_ = s_extFillAddrPassed < s_extAddrAvail
                       || s_extFillVendorPassed < s_extVendorAvail
                       || s_extFillBinaryPassed < s_extBinaryAvail;
            counts->vendorBinarySize = s_extReportedBinaryOverride ?? nb;
            if (s_extSecondResult == VkResult.VK_SUCCESS && short_ && s_extConformingIncomplete)
                return VkResult.VK_INCOMPLETE;
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
    /// <c>WriteCap</c> limits how many elements a fill call writes;
    /// <c>NoWrite</c> makes a fill return <c>Result</c> without touching the
    /// array or <c>*pFaultCounts</c>.</summary>
    private readonly record struct ReportsStep(VkResult? Result = null, int? WriteCap = null, bool NoWrite = false);

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
            if (scripted && step.NoWrite) return forced ?? VkResult.VK_SUCCESS;

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
    // #245: the full shader-abort message buffer to report when the abort
    // struct is chained.
    private static byte[]   s_abortData = [];
    // Overrides the size the query reports (default: s_abortData.Length).
    private static ulong?   s_abortReportedSizeOnQuery;
    // What a fill writes back to messageDataSize; null leaves it untouched
    // (the spec does not promise a write-back on a fill, B4). Defaults to
    // s_abortData.Length.
    private static ulong?   s_abortReportedSizeOnFill;

    // Recorders.
    private static int                   s_debugCalls;
    private static List<VkStructureType> s_debugSTypes = [];
    private static List<nint>            s_debugPNexts = [];
    private static uint                  s_debugSizeOnQuery;
    private static uint                  s_debugSizePassedOnFill;
    private static bool                  s_debugFillVendorPtrNull;
    private static List<bool>            s_abortChained = [];
    private static List<VkStructureType> s_abortSTypes = [];
    private static List<nint>            s_abortPNexts = [];
    private static ulong                 s_abortSizeOnQuery;
    private static bool                  s_abortQueryPtrNull;
    private static ulong                 s_abortSizePassedOnFill;
    private static bool                  s_abortFillPtrNull;
    private static bool                  s_abortPtrAligned8;

    private static void ArrangeDebug(
        uint     length    = 0,
        VkResult first     = VkResult.VK_SUCCESS,
        VkResult second    = VkResult.VK_SUCCESS,
        byte[]?  abortData = null)
    {
        s_fakeException            = null;
        s_debugLength              = length;
        s_debugFirstResult         = first;
        s_debugSecondResult        = second;
        s_abortData                = abortData ?? [];
        s_abortReportedSizeOnQuery = null;
        s_abortReportedSizeOnFill  = (ulong)s_abortData.Length;
        s_debugCalls               = 0;
        s_debugSTypes              = [];
        s_debugPNexts              = [];
        s_debugSizeOnQuery         = uint.MaxValue;
        s_debugSizePassedOnFill    = uint.MaxValue;
        s_debugFillVendorPtrNull   = false;
        s_abortChained             = [];
        s_abortSTypes              = [];
        s_abortPNexts              = [];
        s_abortSizeOnQuery         = ulong.MaxValue;
        s_abortQueryPtrNull        = false;
        s_abortSizePassedOnFill    = ulong.MaxValue;
        s_abortFillPtrNull         = false;
        s_abortPtrAligned8         = false;
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

            var abort = (VkDeviceFaultShaderAbortMessageInfoKHR*)info->pNext;
            s_abortChained.Add(abort != null);
            if (abort != null)
            {
                s_abortSTypes.Add(abort->sType);
                s_abortPNexts.Add((nint)abort->pNext);
            }

            // A query passes no buffer at all; a fill passes at least one
            // (a message-only fill has a null vendor pointer).
            bool isQuery = info->pVendorBinaryData == null && (abort == null || abort->pMessageData == null);
            if (isQuery)
            {
                s_debugSizeOnQuery = info->vendorBinarySize;
                if (abort != null)
                {
                    s_abortSizeOnQuery  = abort->messageDataSize;
                    s_abortQueryPtrNull = abort->pMessageData == null;
                }
                if ((int)r < 0) return r;
                info->vendorBinarySize = s_debugLength;
                if (abort != null)
                    abort->messageDataSize = s_abortReportedSizeOnQuery ?? (ulong)s_abortData.Length;
                return r;
            }

            s_debugSizePassedOnFill  = info->vendorBinarySize;
            s_debugFillVendorPtrNull = info->pVendorBinaryData == null;
            if (abort != null)
            {
                s_abortSizePassedOnFill = abort->messageDataSize;
                s_abortFillPtrNull      = abort->pMessageData == null;
                s_abortPtrAligned8      = ((nint)abort->pMessageData & 7) == 0;
            }
            if ((int)r < 0) return r;

            uint n = info->pVendorBinaryData == null ? 0 : Math.Min(info->vendorBinarySize, s_debugLength);
            byte* p = (byte*)info->pVendorBinaryData;
            for (uint j = 0; j < n; j++)
                p[j] = (byte)j;
            info->vendorBinarySize = n;
            bool shortFill = s_debugSizePassedOnFill < s_debugLength;

            if (abort != null)
            {
                // Never write more than was passed.
                ulong m = abort->pMessageData == null
                    ? 0
                    : Math.Min(abort->messageDataSize, (ulong)s_abortData.Length);
                s_abortData.AsSpan(0, (int)m).CopyTo(new Span<byte>(abort->pMessageData, (int)m));
                shortFill |= s_abortSizePassedOnFill < (ulong)s_abortData.Length;
                if (s_abortReportedSizeOnFill is { } reported)
                    abort->messageDataSize = reported;
            }

            // A conforming driver answers VK_INCOMPLETE when passed too little.
            if (r == VkResult.VK_SUCCESS && shortFill)
                return VkResult.VK_INCOMPLETE;
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

    /// <summary>Builds the spec's shader-abort framing: for each payload a
    /// 64-bit little-endian size, the payload, then zero padding to the next
    /// 8-byte boundary.</summary>
    private static byte[] Frame(params byte[][] payloads)
    {
        var bytes = new List<byte>();
        foreach (byte[] payload in payloads)
        {
            bytes.AddRange(BitConverter.GetBytes((ulong)payload.Length));
            bytes.AddRange(payload);
            while (bytes.Count % 8 != 0) bytes.Add(0);
        }
        return bytes.ToArray();
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
    // EXT (13)
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

    [Fact]
    public void Ext_CountsAboveCap_AreCapped_PassedAsCap_AndIncomplete()
    {
        ArrangeExt(addresses: 1_000_000, vendors: 1_000_000);

        VkResult r = DeviceFaultReader.ReadExt(&FakeExt, FakeDevice, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.Equal(DeviceFaultReader.MaxAddressInfos, s_extFillAddrPassed);
        Assert.Equal(DeviceFaultReader.MaxVendorInfos, s_extFillVendorPassed);
        Assert.Equal(VkResult.VK_INCOMPLETE, r);
        Assert.NotNull(report);
        Assert.True(report.IsIncomplete);
        DeviceFaultEntry entry = Assert.Single(report.Entries);
        Assert.Equal((int)DeviceFaultReader.MaxAddressInfos, entry.AddressInfos.Length);
        Assert.Equal((int)DeviceFaultReader.MaxVendorInfos, entry.VendorInfos.Length);
    }

    [Fact]
    public void Ext_BinarySizeAboveCap_IsCapped_PassedAsCap_AndIncomplete()
    {
        ArrangeExt(binary: 1UL << 40); // a garbage 1 TiB size

        VkResult r = DeviceFaultReader.ReadExt(&FakeExt, FakeDevice, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.Equal((ulong)DeviceFaultReader.MaxVendorBinaryBytes, s_extFillBinaryPassed);
        Assert.Equal(VkResult.VK_INCOMPLETE, r);
        Assert.NotNull(report);
        Assert.True(report.IsIncomplete);
        Assert.Equal((int)DeviceFaultReader.MaxVendorBinaryBytes, report.VendorBinary.Length);
    }

    [Fact]
    public void Ext_CapExceeded_DriverAnswersSuccess_StillIncomplete()
    {
        ArrangeExt(addresses: DeviceFaultReader.MaxAddressInfos + 1);
        s_extConformingIncomplete = false; // a driver that says VK_SUCCESS anyway

        VkResult r = DeviceFaultReader.ReadExt(&FakeExt, FakeDevice, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_SUCCESS, r);
        Assert.NotNull(report);
        Assert.True(report.IsIncomplete);
        Assert.Equal((int)DeviceFaultReader.MaxAddressInfos, Assert.Single(report.Entries).AddressInfos.Length);
    }

    // =====================================================================
    // KHR entries (12)
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

    [Fact]
    public void Khr_FillTimeout_TranslatesNothing_StopsDraining()
    {
        ArrangeReports(
            MakeKhr(DeviceFaultFlags.None),
            MakeKhr(DeviceFaultFlags.None),
            MakeKhr(DeviceFaultFlags.None));
        // The fill times out and leaves *pFaultCounts at the requested 3 —
        // trusting it would emit three blank entries.
        s_reportsScript[1] = new ReportsStep(Result: VkResult.VK_TIMEOUT, NoWrite: true);

        VkResult r = DeviceFaultReader.ReadKhrEntries(&FakeReports, FakeDevice, 0,
            out DeviceFaultEntry[] entries, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_TIMEOUT, r);
        Assert.Empty(entries);
        Assert.False(incomplete);
        Assert.Equal(1, s_reportsFillCalls);
        Assert.Equal(2, s_reportsCalls); // no further round
        Assert.Equal(3, s_queue.Count);
    }

    /// <summary>
    /// #244: the no-fault per-frame poll is one count call answering
    /// <c>VK_TIMEOUT</c>; it must hand back the shared empty array, not a
    /// fresh one.
    /// </summary>
    [Fact]
    public void Khr_NoFault_ReturnsSharedEmptyArray()
    {
        ArrangeReports();

        VkResult r = DeviceFaultReader.ReadKhrEntries(&FakeReports, FakeDevice, 0,
            out DeviceFaultEntry[] entries, out _);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_TIMEOUT, r);
        Assert.Same(Array.Empty<DeviceFaultEntry>(), entries);
    }

    // =====================================================================
    // KHR debug info, abort chaining off (5)
    // =====================================================================

    [Fact]
    public void KhrDebug_SizeThenFill_WritesBackSize_SetsSTypeNullPNext()
    {
        ArrangeDebug(length: 100);

        VkResult r = DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            readShaderAbortMessages: false, out byte[] binary, out _, out bool incomplete);

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
            readShaderAbortMessages: false, out byte[] binary, out _, out bool incomplete);

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
            readShaderAbortMessages: false, out byte[] binary, out _, out bool incomplete);

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
            readShaderAbortMessages: false, out byte[] binary, out _, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_ERROR_NOT_ENOUGH_SPACE_KHR, r);
        Assert.NotNull(binary);
        Assert.Empty(binary);
        Assert.False(incomplete);
    }

    [Fact]
    public void KhrDebug_SizeAboveCap_IsCapped_PassedAsCap_AndIncomplete()
    {
        ArrangeDebug(length: uint.MaxValue); // a garbage 4 GiB size

        VkResult r = DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            readShaderAbortMessages: false, out byte[] binary, out _, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(DeviceFaultReader.MaxVendorBinaryBytes, s_debugSizePassedOnFill);
        Assert.Equal(VkResult.VK_INCOMPLETE, r);
        Assert.True(incomplete);
        Assert.Equal((int)DeviceFaultReader.MaxVendorBinaryBytes, binary.Length);
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
        bool ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, new DeviceFaultLog(), out DeviceFaultReport? report);

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
        bool ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, new DeviceFaultLog(), out DeviceFaultReport? report);

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

        bool ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, new DeviceFaultLog(), out DeviceFaultReport? report);

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
            extOk = DeviceFaultReader.TryRead(in ext, FakeDevice, 0, new DeviceFaultLog(), out extReport);
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
            khrOk = DeviceFaultReader.TryRead(in khr, FakeDevice, 0, new DeviceFaultLog(), out khrReport);
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
            ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, new DeviceFaultLog(), out report);
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
            ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, new DeviceFaultLog(), out report);
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
            $"Device.TryGetDeviceFault: vkGetDeviceFaultDebugInfoKHR returned {VkResult.VK_ERROR_NOT_ENOUGH_SPACE_KHR}; the report carries no vendor binary and no shader abort messages.",
            warning.Message);
    }

    // =====================================================================
    // TryRead + fault log (#244) (6)
    // =====================================================================

    private static DeviceFaultEntry LoggedEntry(ulong groupId, DeviceFaultFlags flags = DeviceFaultFlags.None)
        => new() { GroupId = groupId, Flags = flags, Description = "logged" };

    private static ulong[] GroupIds(DeviceFaultEntry[] entries)
    {
        var ids = new ulong[entries.Length];
        for (int i = 0; i < entries.Length; i++)
            ids[i] = entries[i].GroupId;
        return ids;
    }

    [Fact]
    public void TryRead_Khr_LogEntriesPrecedeFreshEntries()
    {
        var log = new DeviceFaultLog();
        log.Append([LoggedEntry(1), LoggedEntry(2)]);
        ArrangeReports(MakeKhr(DeviceFaultFlags.DeviceLost, groupId: 3));
        ArrangeDebug();
        DeviceFaultEntryPoints eps = KhrOnly();

        bool ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, log, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.True(ok);
        Assert.Equal([1UL, 2, 3], GroupIds(report!.Entries));
        Assert.Equal(DeviceFaultFlags.DeviceLost, report.Entries[2].Flags);
        Assert.False(report.IsIncomplete);
        Assert.Equal(3, log.Count);
    }

    [Fact]
    public void TryRead_Khr_RepeatCall_ReturnsSameEntries()
    {
        var log = new DeviceFaultLog();
        ArrangeReports(
            MakeKhr(DeviceFaultFlags.MemoryAddress, groupId: 4),
            MakeKhr(DeviceFaultFlags.DeviceLost, groupId: 4));
        ArrangeDebug();
        DeviceFaultEntryPoints eps = KhrOnly();

        bool firstOk = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, log, out DeviceFaultReport? first);
        int fillCallsAfterFirst = s_reportsFillCalls;
        bool secondOk = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, log, out DeviceFaultReport? second);

        AssertNoFakeException();
        Assert.True(firstOk);
        Assert.True(secondOk);
        Assert.Equal(fillCallsAfterFirst, s_reportsFillCalls); // the second read drained nothing
        Assert.Equal(2, first!.Entries.Length);
        Assert.Equal(first.Entries.Length, second!.Entries.Length);
        for (int i = 0; i < first.Entries.Length; i++)
            Assert.Same(first.Entries[i], second.Entries[i]);
    }

    [Fact]
    public void TryRead_Khr_DrainErrorWithNonEmptyLog_ReturnsTrue_Incomplete()
    {
        var log = new DeviceFaultLog();
        log.Append([LoggedEntry(1, DeviceFaultFlags.DeviceLost)]);
        ArrangeReports(MakeKhr(DeviceFaultFlags.None));
        s_reportsScript[0] = new ReportsStep(Result: VkResult.VK_ERROR_UNKNOWN);
        ArrangeDebug();
        DeviceFaultEntryPoints eps = KhrOnly();

        var warnings = InstallDeviceSink(out DiagnosticSink original);
        bool ok;
        DeviceFaultReport? report;
        try
        {
            ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, log, out report);
        }
        finally
        {
            AhjoDiagnostics.Sink = original;
        }

        AssertNoFakeException();
        Assert.True(ok);
        DeviceFaultEntry entry = Assert.Single(report!.Entries);
        Assert.Equal(1UL, entry.GroupId);
        Assert.True(report.IsIncomplete);
        var warning = Assert.Single(warnings);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Equal(
            $"Device.TryGetDeviceFault: vkGetDeviceFaultReportsKHR returned {VkResult.VK_ERROR_UNKNOWN}; returning 1 previously drained entries.",
            warning.Message);
    }

    [Fact]
    public void TryRead_Khr_DrainErrorWithEmptyLog_ReturnsFalse()
    {
        var log = new DeviceFaultLog();
        ArrangeReports(MakeKhr(DeviceFaultFlags.None));
        s_reportsScript[0] = new ReportsStep(Result: VkResult.VK_ERROR_UNKNOWN);
        ArrangeDebug(length: 8);
        DeviceFaultEntryPoints eps = KhrOnly();

        var warnings = InstallDeviceSink(out DiagnosticSink original);
        bool ok;
        DeviceFaultReport? report;
        try
        {
            ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, log, out report);
        }
        finally
        {
            AhjoDiagnostics.Sink = original;
        }

        AssertNoFakeException();
        Assert.False(ok);
        Assert.Null(report);
        Assert.Equal(0, log.Count);
        Assert.Equal(0, s_debugCalls);
        var warning = Assert.Single(warnings);
        Assert.Equal(
            $"Device.TryGetDeviceFault: vkGetDeviceFaultReportsKHR returned {VkResult.VK_ERROR_UNKNOWN}; no device-fault report is available.",
            warning.Message);
    }

    [Fact]
    public void TryRead_Khr_LogDropped_SetsIncomplete()
    {
        var log = new DeviceFaultLog();
        var overflow = new DeviceFaultEntry[DeviceFaultLog.Capacity + 1];
        for (int i = 0; i < overflow.Length; i++)
            overflow[i] = LoggedEntry((ulong)i);
        log.Append(overflow);
        Assert.True(log.Dropped);
        ArrangeReports();
        ArrangeDebug();
        DeviceFaultEntryPoints eps = KhrOnly();

        bool ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, log, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.True(ok);
        Assert.True(report!.IsIncomplete);
        Assert.Equal(DeviceFaultLog.Capacity, report.Entries.Length);
    }

    [Fact]
    public void TryRead_Ext_IgnoresLog()
    {
        var log = new DeviceFaultLog();
        log.Append([LoggedEntry(1), LoggedEntry(2)]);
        ArrangeExt(addresses: 1, description: "ext fault");
        DeviceFaultEntryPoints eps = ExtOnly();

        bool ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, log, out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.True(ok);
        Assert.Equal(DeviceFaultApi.Ext, report!.Api);
        Assert.Equal("ext fault", Assert.Single(report.Entries).Description);
        Assert.Equal(2, log.Count);
    }

    // =====================================================================
    // Shader-abort framing parser (#245) (8)
    // =====================================================================

    private static byte[] Utf8Bytes(string s) => System.Text.Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Parse_Empty_ReturnsSharedEmpty()
    {
        byte[][] messages = DeviceFaultReader.ParseShaderAbortMessages([], out bool truncated);

        Assert.Same(Array.Empty<byte[]>(), messages);
        Assert.False(truncated);
    }

    [Fact]
    public void Parse_OneMessage()
    {
        // The measured Slang shape: "bad value: %u\0" padded to 16, then a
        // 4-byte argument.
        var payload = new byte[20];
        Utf8Bytes("bad value: %u").CopyTo(payload, 0);
        BitConverter.GetBytes(42u).CopyTo(payload, 16);

        byte[][] messages = DeviceFaultReader.ParseShaderAbortMessages(Frame(payload), out bool truncated);

        Assert.False(truncated);
        Assert.Equal(payload, Assert.Single(messages));
    }

    [Fact]
    public void Parse_TwoMessages_SecondAtNext8AlignedOffset()
    {
        byte[] first  = Utf8Bytes("thirteen byte"); // 13 bytes
        byte[] second = [0xAA, 0xBB, 0xCC];
        Assert.Equal(13, first.Length);
        byte[] data = Frame(first, second);
        // 8 (size) + 13 (payload) → 21, padded to 24; the second size starts there.
        Assert.Equal(3UL, BitConverter.ToUInt64(data, 24));

        byte[][] messages = DeviceFaultReader.ParseShaderAbortMessages(data, out bool truncated);

        Assert.False(truncated);
        Assert.Equal(2, messages.Length);
        Assert.Equal(first, messages[0]);
        Assert.Equal(second, messages[1]);
    }

    [Fact]
    public void Parse_SizeOverrunsBuffer_KeepsPrefix_Truncated()
    {
        byte[] good = Frame([1, 2, 3]);
        var data = new byte[good.Length + 8 + 4];
        good.CopyTo(data, 0);
        BitConverter.GetBytes(100UL).CopyTo(data, good.Length); // claims 100, 4 remain

        byte[][] messages = DeviceFaultReader.ParseShaderAbortMessages(data, out bool truncated);

        Assert.True(truncated);
        Assert.Equal([1, 2, 3], Assert.Single(messages));
    }

    [Fact]
    public void Parse_TrailingFewerThan8Bytes_Ignored_NotTruncated()
    {
        byte[] framed = Frame([9, 8, 7, 6]);
        var data = new byte[framed.Length + 7];
        framed.CopyTo(data, 0);
        data[^1] = 0xFF; // junk in the tail padding

        byte[][] messages = DeviceFaultReader.ParseShaderAbortMessages(data, out bool truncated);

        Assert.False(truncated);
        Assert.Equal([9, 8, 7, 6], Assert.Single(messages));
    }

    [Fact]
    public void Parse_ZeroLengthPayload_Kept()
    {
        byte[][] messages = DeviceFaultReader.ParseShaderAbortMessages(Frame([], [5]), out bool truncated);

        Assert.False(truncated);
        Assert.Equal(2, messages.Length);
        Assert.Empty(messages[0]);
        Assert.Equal([5], messages[1]);
    }

    [Fact]
    public void Parse_CountCap_StopsAndTruncates()
    {
        // Zero-length messages are 8 bytes each: a zero-filled buffer.
        var data = new byte[(DeviceFaultReader.MaxShaderAbortMessages + 1) * 8];

        byte[][] messages = DeviceFaultReader.ParseShaderAbortMessages(data, out bool truncated);

        Assert.True(truncated);
        Assert.Equal(DeviceFaultReader.MaxShaderAbortMessages, messages.Length);
    }

    /// <summary>
    /// The <c>VK_KHR_shader_abort</c> proposal's sample reader advances 4
    /// bytes after the 8-byte size, so it would read this first payload as
    /// the size's high word followed by the first payload bytes.
    /// </summary>
    [Fact]
    public void Parse_FourByteAdvanceBug_NotReproduced()
    {
        byte[] first  = [0x11, 0x22, 0x33, 0x44, 0x55];
        byte[] second = [0x66, 0x77];
        byte[] data   = Frame(first, second);

        byte[][] messages = DeviceFaultReader.ParseShaderAbortMessages(data, out bool truncated);

        Assert.False(truncated);
        Assert.Equal(2, messages.Length);
        Assert.Equal(first, messages[0]);
        Assert.Equal(second, messages[1]);
    }

    // =====================================================================
    // KHR debug info + shader-abort messages (#245) (10)
    // =====================================================================

    [Fact]
    public void KhrDebug_AbortOff_NoChain()
    {
        ArrangeDebug(length: 16, abortData: Frame([1, 2, 3]));

        VkResult r = DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            readShaderAbortMessages: false, out byte[] binary, out byte[][] messages, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_SUCCESS, r);
        Assert.Equal(2, s_debugCalls);
        Assert.All(s_debugPNexts, p => Assert.Equal(0, p));
        Assert.All(s_abortChained, Assert.False);
        Assert.Equal(16, binary.Length);
        Assert.Same(Array.Empty<byte[]>(), messages);
        Assert.False(incomplete);
    }

    [Fact]
    public void KhrDebug_AbortOn_ChainsStruct_STypeAndNullPNext_QueryPassesZeroAndNull()
    {
        ArrangeDebug(length: 8, abortData: Frame([1, 2, 3]));

        DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            readShaderAbortMessages: true, out _, out _, out _);

        AssertNoFakeException();
        Assert.Equal(2, s_debugCalls);
        Assert.All(s_abortChained, Assert.True);
        Assert.Equal(2, s_abortSTypes.Count);
        Assert.All(s_abortSTypes, s => Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_DEVICE_FAULT_SHADER_ABORT_MESSAGE_INFO_KHR, s));
        Assert.All(s_abortPNexts, p => Assert.Equal(0, p));
        Assert.Equal(0UL, s_abortSizeOnQuery);
        Assert.True(s_abortQueryPtrNull);
        Assert.All(s_debugSTypes, s => Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_DEVICE_FAULT_DEBUG_INFO_KHR, s));
    }

    [Fact]
    public void KhrDebug_AbortOn_MessageOnly_StillFills_VendorNull()
    {
        ArrangeDebug(length: 0, abortData: Frame([1, 2, 3], [4, 5, 6, 7, 8, 9, 10, 11, 12]));

        VkResult r = DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            readShaderAbortMessages: true, out byte[] binary, out byte[][] messages, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_SUCCESS, r);
        Assert.Equal(2, s_debugCalls);
        Assert.True(s_debugFillVendorPtrNull);
        Assert.Equal(0u, s_debugSizePassedOnFill);
        Assert.Empty(binary);
        Assert.Equal(2, messages.Length);
        Assert.Equal([1, 2, 3], messages[0]);
        Assert.Equal([4, 5, 6, 7, 8, 9, 10, 11, 12], messages[1]);
        Assert.False(incomplete);
    }

    [Fact]
    public void KhrDebug_AbortOn_BinaryAndMessages_OneFill()
    {
        byte[] data = Frame([7, 7]);
        ArrangeDebug(length: 40, abortData: data);

        VkResult r = DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            readShaderAbortMessages: true, out byte[] binary, out byte[][] messages, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_SUCCESS, r);
        Assert.Equal(2, s_debugCalls);
        Assert.Equal(40u, s_debugSizePassedOnFill);
        Assert.Equal((ulong)data.Length, s_abortSizePassedOnFill);
        Assert.False(s_debugFillVendorPtrNull);
        Assert.False(s_abortFillPtrNull);
        Assert.Equal(40, binary.Length);
        Assert.Equal([7, 7], Assert.Single(messages));
        Assert.False(incomplete);
    }

    [Fact]
    public void KhrDebug_AbortOn_BothZero_NoFill()
    {
        ArrangeDebug(length: 0);

        VkResult r = DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            readShaderAbortMessages: true, out byte[] binary, out byte[][] messages, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_SUCCESS, r);
        Assert.Equal(1, s_debugCalls);
        Assert.Empty(binary);
        Assert.Same(Array.Empty<byte[]>(), messages);
        Assert.False(incomplete);
    }

    [Fact]
    public void KhrDebug_AbortOn_MessagePointerIs8Aligned()
    {
        // An odd byte count: the buffer is still ulong-backed, so 8-aligned.
        ArrangeDebug(length: 3, abortData: Frame([1, 2, 3, 4, 5]));

        DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            readShaderAbortMessages: true, out _, out _, out _);

        AssertNoFakeException();
        Assert.False(s_abortFillPtrNull);
        Assert.True(s_abortPtrAligned8);
    }

    [Fact]
    public void KhrDebug_AbortOn_SizeAboveCap_PassedAsCap_Incomplete()
    {
        // Allocates the 16 MiB cap once.
        ArrangeDebug(length: 0, abortData: Frame([1, 2, 3]));
        s_abortReportedSizeOnQuery = ulong.MaxValue;

        VkResult r = DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            readShaderAbortMessages: true, out _, out byte[][] messages, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal((ulong)DeviceFaultReader.MaxShaderAbortMessageBytes, s_abortSizePassedOnFill);
        Assert.True((int)r >= 0);
        Assert.True(incomplete);
        Assert.Equal([1, 2, 3], Assert.Single(messages));
    }

    [Fact]
    public void KhrDebug_AbortOn_DriverLeavesSizeUntouched_TrimsToAllocated()
    {
        byte[] data = Frame([1, 2, 3], [4]);
        ArrangeDebug(length: 0, abortData: data);
        s_abortReportedSizeOnFill = null; // the fill does not write messageDataSize back

        VkResult r = DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            readShaderAbortMessages: true, out _, out byte[][] messages, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_SUCCESS, r);
        Assert.Equal((ulong)data.Length, s_abortSizePassedOnFill);
        Assert.Equal(2, messages.Length);
        Assert.Equal([1, 2, 3], messages[0]);
        Assert.Equal([4], messages[1]);
        Assert.False(incomplete);
    }

    [Fact]
    public void KhrDebug_AbortOn_MalformedFraming_KeepsPrefix_Incomplete()
    {
        byte[] good = Frame([1, 2]);
        var data = new byte[good.Length + 16];
        good.CopyTo(data, 0);
        BitConverter.GetBytes(1000UL).CopyTo(data, good.Length); // overruns the buffer
        ArrangeDebug(length: 0, abortData: data);

        VkResult r = DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            readShaderAbortMessages: true, out _, out byte[][] messages, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_SUCCESS, r);
        Assert.Equal([1, 2], Assert.Single(messages));
        Assert.True(incomplete);
    }

    [Fact]
    public void KhrDebug_AbortOn_Error_EmptyBinaryAndMessages()
    {
        ArrangeDebug(length: 16, second: VkResult.VK_ERROR_NOT_ENOUGH_SPACE_KHR, abortData: Frame([1, 2, 3]));

        VkResult r = DeviceFaultReader.ReadKhrDebugInfo(&FakeDebugInfo, FakeDevice,
            readShaderAbortMessages: true, out byte[] binary, out byte[][] messages, out bool incomplete);

        AssertNoFakeException();
        Assert.Equal(VkResult.VK_ERROR_NOT_ENOUGH_SPACE_KHR, r);
        Assert.Empty(binary);
        Assert.Empty(messages);
        Assert.False(incomplete);
    }

    // =====================================================================
    // TryRead + shader-abort messages (#245) (2)
    // =====================================================================

    [Fact]
    public void TryRead_Khr_AbortEnabled_SurfacesMessages()
    {
        ArrangeReports(MakeKhr(DeviceFaultFlags.DeviceLost, groupId: 1));
        ArrangeDebug(length: 8, abortData: Frame([0xA1, 0xA2], [0xB1]));
        DeviceFaultEntryPoints eps = new(null, &FakeReports, &FakeDebugInfo, shaderAbortMessages: true);
        Assert.True(eps.ShaderAbortMessages);

        bool ok = DeviceFaultReader.TryRead(in eps, FakeDevice, 0, new DeviceFaultLog(), out DeviceFaultReport? report);

        AssertNoFakeException();
        Assert.True(ok);
        Assert.Single(report!.Entries);
        Assert.Equal(8, report.VendorBinary.Length);
        Assert.Equal(2, report.ShaderAbortMessages.Length);
        Assert.Equal([0xA1, 0xA2], report.ShaderAbortMessages[0]);
        Assert.Equal([0xB1], report.ShaderAbortMessages[1]);
        Assert.False(report.IsIncomplete);
    }

    [Fact]
    public void TryRead_Khr_EntryPointsFlagFalse_WhenDebugInfoNull()
    {
        DeviceFaultEntryPoints eps = new(null, &FakeReports, null, true);

        Assert.False(eps.ShaderAbortMessages);
        Assert.False(KhrOnly().ShaderAbortMessages);
    }
}
