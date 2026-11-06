using System.Runtime.InteropServices;
using System.Text;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Serializes every test that touches <see cref="PclStats"/>' process-wide
/// static state (issue #261).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PclStatsCollection
{
    public const string Name = "PclStats";
}

/// <summary>
/// Issue #261 spec D6/D9: the PCL Stats provider. Windows-only, driverless —
/// ETW needs no GPU.
/// </summary>
[Collection(PclStatsCollection.Name)]
public sealed class PclStatsTests
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private const byte InUInt32 = 8;
    private const byte InUInt64 = 10;

    // Independent TraceLogging metadata encoder: UInt16 LE total size, then
    // (events only) a 0x00 tag byte, the name and NUL, then per field the
    // name, NUL and the InType byte.
    private static byte[] BuildEvent(string name, params (string Name, byte InType)[] fields)
    {
        var body = new List<byte> { 0x00 };
        body.AddRange(Encoding.ASCII.GetBytes(name));
        body.Add(0);
        foreach (var (fieldName, inType) in fields)
        {
            body.AddRange(Encoding.ASCII.GetBytes(fieldName));
            body.Add(0);
            body.Add(inType);
        }
        return WithSizePrefix(body);
    }

    private static byte[] BuildTraits(string providerName)
    {
        var body = new List<byte>();
        body.AddRange(Encoding.ASCII.GetBytes(providerName));
        body.Add(0);
        return WithSizePrefix(body);
    }

    private static byte[] WithSizePrefix(List<byte> body)
    {
        int total = body.Count + 2;
        var result = new byte[total];
        result[0] = (byte)(total & 0xFF);
        result[1] = (byte)(total >> 8);
        body.CopyTo(result, 2);
        return result;
    }

    [Fact]
    public void MetadataBlobs_MatchBuilder()
    {
        Assert.Equal(BuildTraits("PCLStatsTraceLoggingProvider"), PclStatsEtw.ProviderTraits.ToArray());
        Assert.Equal(BuildEvent("PCLStatsInit"), PclStatsEtw.InitEvent.ToArray());
        Assert.Equal(BuildEvent("PCLStatsEvent", ("Marker", InUInt32), ("FrameID", InUInt64)),
            PclStatsEtw.MarkerEvent.ToArray());
        Assert.Equal(BuildEvent("PCLStatsInput", ("IdThread", InUInt32)), PclStatsEtw.InputIdThreadEvent.ToArray());
        Assert.Equal(BuildEvent("PCLStatsInput", ("VirtualKey", InUInt32)), PclStatsEtw.InputVirtualKeyEvent.ToArray());
        Assert.Equal(BuildEvent("PCLStatsInput", ("MsgId", InUInt32)), PclStatsEtw.InputMsgIdEvent.ToArray());
        Assert.Equal(BuildEvent("PCLStatsFlags", ("Flags", InUInt32)), PclStatsEtw.FlagsEvent.ToArray());
        Assert.Equal(BuildEvent("PCLStatsShutdown"), PclStatsEtw.ShutdownEvent.ToArray());
    }

    [Fact]
    public unsafe void Descriptors_AreSixteenBytes_AndVerboseOnTraceLoggingChannel()
    {
        Assert.Equal(16, sizeof(PclStatsEtw.EventDescriptor));
        Assert.Equal(16, sizeof(PclStatsEtw.EventDataDescriptor));
        Assert.Equal(11, PclStatsEtw.Descriptor.Channel);
        Assert.Equal(5, PclStatsEtw.Descriptor.Level);
        Assert.Equal(0ul, PclStatsEtw.Descriptor.Keyword);
        Assert.Equal(new Guid("0d216f06-82a6-4d49-bc4f-8f38ae56efab"), PclStatsEtw.ProviderId);
    }

    [Fact]
    public void Initialize_Marker_Shutdown_Succeeds()
    {
        TestGate.RequirePlatform(IsWindows, "PCL Stats is ETW, which is Windows-only.");
        PclStats.Initialize(new PclStatsOptions());
        try
        {
            Assert.True(PclStats.IsInitialized);
            Assert.NotEqual(0u, PclStats.PingMessageId);
            Assert.True(PclStats.IsPingMessage(PclStats.PingMessageId));
            PclStats.Marker(PclMarker.SimulationStart, 1);
            PclStats.Marker(PclMarker.SimulationEnd, 1);
        }
        finally
        {
            PclStats.Shutdown();
        }
        Assert.False(PclStats.IsInitialized);
        Assert.Equal(0u, PclStats.PingMessageId);
    }

    [Fact]
    public void Initialize_Twice_Throws()
    {
        TestGate.RequirePlatform(IsWindows, "PCL Stats is ETW, which is Windows-only.");
        PclStats.Initialize(new PclStatsOptions());
        try
        {
            Assert.Throws<InvalidOperationException>(() => PclStats.Initialize(new PclStatsOptions()));
        }
        finally
        {
            PclStats.Shutdown();
        }
    }

    [Fact]
    public void Shutdown_WithoutInitialize_IsNoOp()
    {
        PclStats.Shutdown();
        Assert.False(PclStats.IsInitialized);
    }

    [Fact]
    public void Marker_BeforeInitialize_IsNoOp()
    {
        Assert.False(PclStats.IsInitialized);
        int pings = PclStats.PingMarkersWrittenForTesting;
        PclStats.NotifyPingReceived();
        PclStats.Marker(PclMarker.SimulationStart, 1);
        Assert.Equal(pings, PclStats.PingMarkersWrittenForTesting);
    }

    [Fact]
    public void Marker_ZeroFrameId_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PclStats.Marker(PclMarker.SimulationStart, 0));
    }

    [Fact]
    public void PendingPing_IsEmittedBeforeSimulationStart_Once()
    {
        TestGate.RequirePlatform(IsWindows, "PCL Stats is ETW, which is Windows-only.");
        PclStats.Initialize(new PclStatsOptions());
        try
        {
            PclStats.SetProviderEnabledForTesting(true);
            int before = PclStats.PingMarkersWrittenForTesting;

            // A non-SimulationStart marker does not consume the ping.
            PclStats.NotifyPingReceived();
            PclStats.Marker(PclMarker.PresentStart, 4);
            Assert.Equal(before, PclStats.PingMarkersWrittenForTesting);

            PclStats.Marker(PclMarker.SimulationStart, 5);
            PclStats.Marker(PclMarker.SimulationStart, 5);
            Assert.Equal(before + 1, PclStats.PingMarkersWrittenForTesting);
        }
        finally
        {
            PclStats.SetProviderEnabledForTesting(false);
            PclStats.Shutdown();
        }
    }

    [Fact]
    public void PendingPing_IsConsumed_EvenWithNoListener()
    {
        TestGate.RequirePlatform(IsWindows, "PCL Stats is ETW, which is Windows-only.");
        PclStats.Initialize(new PclStatsOptions());
        try
        {
            PclStats.SetProviderEnabledForTesting(false);
            int before = PclStats.PingMarkersWrittenForTesting;
            PclStats.NotifyPingReceived();
            PclStats.Marker(PclMarker.SimulationStart, 1);   // consumes, writes nothing
            PclStats.SetProviderEnabledForTesting(true);
            PclStats.Marker(PclMarker.SimulationStart, 2);   // no stale ping
            Assert.Equal(before, PclStats.PingMarkersWrittenForTesting);
        }
        finally
        {
            PclStats.SetProviderEnabledForTesting(false);
            PclStats.Shutdown();
        }
    }

    [Fact]
    public void Marker_IsAllocationFree()
    {
        TestGate.RequirePlatform(IsWindows, "PCL Stats is ETW, which is Windows-only.");
        PclStats.Initialize(new PclStatsOptions());
        try
        {
            PclStats.SetProviderEnabledForTesting(true);
            for (ulong i = 1; i <= 1000; i++)
            {
                PclStats.NotifyPingReceived();
                PclStats.Marker(PclMarker.SimulationStart, i);
                PclStats.Marker(PclMarker.OutOfBandPresentStart, i);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (ulong i = 1; i <= 10_000; i++)
            {
                if ((i & 7) == 0) PclStats.NotifyPingReceived();
                PclStats.Marker((i & 1) == 0 ? PclMarker.SimulationStart : PclMarker.PresentEnd, i);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(0, after - before);
        }
        finally
        {
            PclStats.SetProviderEnabledForTesting(false);
            PclStats.Shutdown();
        }
    }

    [Fact]
    public void IsPingMessage_ZeroIsFalse()
    {
        Assert.False(PclStats.IsPingMessage(0));
    }
}
