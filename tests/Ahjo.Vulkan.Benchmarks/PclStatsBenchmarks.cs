using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Issue #261: <see cref="PclStats.Marker"/> runs six to ten times per frame
/// and must allocate nothing. Runs on any Windows host — PCL Stats is ETW and
/// needs no GPU. <c>Marker_ListenerForced</c> measures the real
/// <c>EventWriteTransfer</c> path by forcing the provider enabled without an
/// ETW session.
/// </summary>
[MemoryDiagnoser]
public class PclStatsBenchmarks
{
    private const int MarkersPerInvoke = 64;

    private ulong _frameId;

    [GlobalSetup]
    public void Setup()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("PclStatsBenchmarks needs Windows (ETW).");
        PclStats.Initialize(new PclStatsOptions());
        if (!PclStats.IsInitialized)
            throw new InvalidOperationException("PclStats.Initialize did not register the provider.");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        PclStats.SetProviderEnabledForTesting(false);
        PclStats.Shutdown();
    }

    [IterationSetup(Target = nameof(Marker_NoListener))]
    public void DisableListener() => PclStats.SetProviderEnabledForTesting(false);

    [IterationSetup(Targets = [nameof(Marker_ListenerForced), nameof(SimulationStart_WithPendingPing)])]
    public void ForceListener() => PclStats.SetProviderEnabledForTesting(true);

    [Benchmark(OperationsPerInvoke = MarkersPerInvoke)]
    public void Marker_NoListener()
    {
        ulong id = ++_frameId;
        for (int i = 0; i < MarkersPerInvoke; i++)
            PclStats.Marker(PclMarker.PresentStart, id);
    }

    [Benchmark(OperationsPerInvoke = MarkersPerInvoke)]
    public void Marker_ListenerForced()
    {
        ulong id = ++_frameId;
        for (int i = 0; i < MarkersPerInvoke; i++)
            PclStats.Marker(PclMarker.PresentStart, id);
    }

    [Benchmark]
    public void SimulationStart_WithPendingPing()
    {
        PclStats.NotifyPingReceived();
        PclStats.Marker(PclMarker.SimulationStart, ++_frameId);
    }
}
