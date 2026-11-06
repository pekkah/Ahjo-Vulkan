using System.Security.Principal;
using Ahjo.Vulkan.Testing;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Issue #261 spec D9: the real oracle for the hand-encoded TraceLogging
/// metadata. Opens a real-time ETW session on the PCL Stats GUID at verbose
/// level, the way PresentMon does, and decodes what <see cref="PclStats"/>
/// writes through TraceEvent's self-describing (TraceLogging) parser.
/// </summary>
[Collection(PclStatsCollection.Name)]
public sealed class PclStatsEtwRoundTripTests
{
    private static readonly Guid PclProvider = new("0d216f06-82a6-4d49-bc4f-8f38ae56efab");

    private static bool IsElevated =>
        OperatingSystem.IsWindows()
        && new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    private sealed record Seen(string EventName, string ProviderName, object? Marker, object? FrameId);

    [Fact]
    public void Events_DecodeAsPclStatsTraceLogging()
    {
        TestGate.RequirePlatform(OperatingSystem.IsWindows() && IsElevated, "ETW real-time sessions need elevation.");

        var seen = new List<Seen>();
        using var shutdownSeen = new ManualResetEventSlim(false);
        string sessionName = "AhjoPclStatsRoundTrip_" + Environment.ProcessId;

        using (var session = new TraceEventSession(sessionName) { StopOnDispose = true })
        {
            session.Source.Dynamic.All += e =>
            {
                if (e.ProviderGuid != PclProvider) return;
                object? marker  = e.PayloadIndex("Marker")  >= 0 ? e.PayloadByName("Marker")  : null;
                object? frameId = e.PayloadIndex("FrameID") >= 0 ? e.PayloadByName("FrameID") : null;
                lock (seen) seen.Add(new Seen(e.EventName, e.ProviderName, marker, frameId));
                if (e.EventName == "PCLStatsShutdown") shutdownSeen.Set();
            };
            session.EnableProvider(PclProvider, TraceEventLevel.Verbose);

            var pump = new Thread(() => session.Source.Process()) { IsBackground = true };
            pump.Start();

            PclStats.Initialize(new PclStatsOptions());
            try
            {
                PclStats.Marker(PclMarker.SimulationStart, 42);
                PclStats.Marker(PclMarker.OutOfBandPresentStart, 42);
            }
            finally
            {
                PclStats.Shutdown();
            }

            Assert.True(shutdownSeen.Wait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken), "PCLStatsShutdown was not delivered within 15 s.");
            session.Stop();
            pump.Join(TimeSpan.FromSeconds(5));
        }

        List<Seen> events;
        lock (seen) events = [.. seen];

        Assert.All(events, e => Assert.Equal("PCLStatsTraceLoggingProvider", e.ProviderName));
        Assert.Contains(events, e => e.EventName == "PCLStatsInit");
        Assert.Contains(events, e => e.EventName == "PCLStatsShutdown");

        var markers = events.FindAll(e => e.EventName == "PCLStatsEvent");
        Assert.Equal(2, markers.Count);
        Assert.Equal(0u,  Convert.ToUInt32(markers[0].Marker, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(11u, Convert.ToUInt32(markers[1].Marker, System.Globalization.CultureInfo.InvariantCulture));
        Assert.All(markers, m => Assert.Equal(42ul, Convert.ToUInt64(m.FrameId, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
