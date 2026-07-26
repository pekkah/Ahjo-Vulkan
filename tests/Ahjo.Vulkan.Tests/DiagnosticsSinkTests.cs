using System.Collections.Concurrent;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Issue #120: every diagnostic the wrapper used to write to stderr flows
/// through the process-wide <see cref="AhjoDiagnostics.Sink"/>. The sink
/// is global state — tests swap it in <c>try/finally</c>, which is safe
/// because the suite runs with parallelization disabled
/// (<c>xunit.runner.json</c>).
/// </summary>
public sealed class DiagnosticsSinkTests
{
    [Fact]
    public void Sink_DefaultsToStdErrWriter()
    {
        // Delegate equality (same static method, null target) — a method
        // group conversion materializes a distinct instance per call site,
        // so reference identity would be the wrong assertion.
        Assert.Equal((DiagnosticSink)AhjoDiagnostics.WriteToStdError, AhjoDiagnostics.Sink);
    }

    [Fact]
    public void Sink_AssigningNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AhjoDiagnostics.Sink = null!);
        // A failed assignment must not have clobbered the active sink.
        Assert.NotNull(AhjoDiagnostics.Sink);
    }

    [Fact]
    public void Sink_Replacement_CapturesWrites_AndRestores()
    {
        var captured = new List<(DiagnosticSeverity Severity, string Source, string Message)>();
        DiagnosticSink original = AhjoDiagnostics.Sink;
        try
        {
            AhjoDiagnostics.Sink = (severity, source, message) => captured.Add((severity, source, message));
            AhjoDiagnostics.Write(DiagnosticSeverity.Warning, "Test", "hello sink");

            var entry = Assert.Single(captured);
            Assert.Equal(DiagnosticSeverity.Warning, entry.Severity);
            Assert.Equal("Test", entry.Source);
            Assert.Equal("hello sink", entry.Message);
        }
        finally
        {
            AhjoDiagnostics.Sink = original;
        }

        Assert.Same(original, AhjoDiagnostics.Sink);
    }

    [Fact]
    public void DefaultSink_WritesMessageVerbatim_ToConsoleError()
    {
        TextWriter original = Console.Error;
        try
        {
            using var writer = new StringWriter();
            Console.SetError(writer);
            AhjoDiagnostics.WriteToStdError(DiagnosticSeverity.Error, "Test", "stderr line");
            Assert.Equal($"stderr line{Environment.NewLine}", writer.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    /// <summary>
    /// End-to-end through a real wrapper call site: a pipeline-cache file
    /// with a garbage header used to print the mismatch warning straight
    /// to stderr; it must now arrive at the installed sink with the same
    /// message text.
    /// </summary>
    [Fact]
    public void PipelineCache_HeaderMismatch_RoutesThroughSink()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        string path = Path.Combine(Path.GetTempPath(), $"ahjo_cache_{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, new byte[64]); // headerSize == 0 → guaranteed mismatch

        var captured = new ConcurrentQueue<(DiagnosticSeverity Severity, string Source, string Message)>();
        DiagnosticSink original = AhjoDiagnostics.Sink;
        try
        {
            AhjoDiagnostics.Sink = (severity, source, message) => captured.Enqueue((severity, source, message));
            using var cache = device.LoadOrCreatePipelineCache(path);
        }
        finally
        {
            AhjoDiagnostics.Sink = original;
            File.Delete(path);
        }

        var mismatch = Assert.Single(captured, e => e.Source == "PipelineCache");
        Assert.Equal(DiagnosticSeverity.Warning, mismatch.Severity);
        Assert.Contains("does not match this device", mismatch.Message);
    }

    /// <summary>
    /// The volatile-swap contract: replacing the sink while another thread
    /// writes must never tear, NRE, or drop into a half-installed state.
    /// </summary>
    [Fact]
    public async Task Sink_ConcurrentSwapAndWrite_DoesNotThrow()
    {
        DiagnosticSink original = AhjoDiagnostics.Sink;
        try
        {
            DiagnosticSink a = static (_, _, _) => { };
            DiagnosticSink b = static (_, _, _) => { };
            using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

            var writer = Task.Run(() =>
            {
                while (!stop.IsCancellationRequested)
                    AhjoDiagnostics.Write(DiagnosticSeverity.Info, "Test", "concurrent");
            }, TestContext.Current.CancellationToken);
            var swapper = Task.Run(() =>
            {
                bool flip = false;
                while (!stop.IsCancellationRequested)
                    AhjoDiagnostics.Sink = (flip = !flip) ? a : b;
            }, TestContext.Current.CancellationToken);

            await Task.WhenAll(writer, swapper);
        }
        finally
        {
            AhjoDiagnostics.Sink = original;
        }
    }

    private static Device CreateGraphicsDevice(Instance instance)
    {
        uint family = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    family = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }
}
