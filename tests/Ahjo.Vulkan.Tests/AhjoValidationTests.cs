using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Issue #122: the scattered debug-only checks (PipelineLayout compatibility
/// in <see cref="CommandRecorder"/>, pool ownership scans, the double-dispose
/// registry) now sit behind one runtime switch, <see cref="AhjoValidation"/>.
/// A failing check throws <see cref="AhjoValidationException"/> and reports
/// through <see cref="AhjoDiagnostics"/>, so it surfaces identically in Debug
/// and Release — which lets a user flip validation on in a Release build to
/// chase a bug.
///
/// <see cref="AhjoValidation.Enabled"/> is process-global; tests swap it in
/// <c>try/finally</c>, safe because the suite runs single-threaded
/// (<c>xunit.runner.json</c>: <c>maxParallelThreads = 1</c>). Tests only ever
/// force it <em>on</em> for the duration; the one disabled-path test restores
/// immediately and never double-disposes a real handle while off.
/// </summary>
public sealed class AhjoValidationTests
{
    [Fact]
    public void Enabled_RoundTrips()
    {
        bool original = AhjoValidation.Enabled;
        try
        {
            AhjoValidation.Enabled = true;
            Assert.True(AhjoValidation.Enabled);
            AhjoValidation.Enabled = false;
            Assert.False(AhjoValidation.Enabled);
        }
        finally
        {
            AhjoValidation.Enabled = original;
        }
    }

    [Fact]
    public void Fail_Throws_AndReportsThroughDiagnosticsSink()
    {
        var captured = new List<(DiagnosticSeverity Severity, string Source, string Message)>();
        DiagnosticSink originalSink = AhjoDiagnostics.Sink;
        try
        {
            AhjoDiagnostics.Sink = (severity, source, message) => captured.Add((severity, source, message));

            var ex = Assert.Throws<AhjoValidationException>(
                () => AhjoValidation.Fail("UnitTest", "boom"));

            Assert.Equal("boom", ex.Message);
            var entry = Assert.Single(captured);
            Assert.Equal(DiagnosticSeverity.Error, entry.Severity);
            Assert.Equal("UnitTest", entry.Source);
            Assert.Equal("boom", entry.Message);
        }
        finally
        {
            AhjoDiagnostics.Sink = originalSink;
        }
    }

    [Fact]
    public void DoubleDispose_OwningHandle_Throws_WhenValidationEnabled()
    {
        TestGate.RequireDriver();

        bool original = AhjoValidation.Enabled;
        using var instance = Instance.Create(default);
        using var device = CreateGraphicsDevice(instance);
        try
        {
            AhjoValidation.Enabled = true;

            // Owning buffer → registered live on create.
            Buffer buffer = device.Allocator.CreateBuffer(
                new BufferDescription { Size = 256, Usage = BufferUsage.TransferSrc },
                new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

            buffer.Dispose(); // first dispose: unregisters, destroys.

            // Second dispose finds the handle no longer live and trips the
            // registry *before* the second vmaDestroyBuffer would run, so the
            // double-free never reaches the driver.
            Assert.Throws<AhjoValidationException>(() => buffer.Dispose());
        }
        finally
        {
            AhjoValidation.Enabled = original;
        }
    }

    [Fact]
    public void SemaphorePool_Release_ForeignHandle_RespectsValidationSwitch()
    {
        TestGate.RequireDriver();

        bool original = AhjoValidation.Enabled;
        using var instance = Instance.Create(default);
        using var device = CreateGraphicsDevice(instance);
        using var poolA = new SemaphorePool(device);
        using var poolB = new SemaphorePool(device);
        try
        {
            BinarySemaphore foreign = poolA.AcquireBinary();

            // Enabled: releasing poolA's handle into poolB is caught by the
            // ownership scan. poolB never takes the handle, so poolA still
            // owns it and destroys it on Dispose.
            AhjoValidation.Enabled = true;
            Assert.Throws<AhjoValidationException>(() => poolB.Release(foreign));

            // Disabled: the scan is skipped — the misuse is silently accepted
            // (the historical Debug.Assert-compiled-out behavior). poolB's
            // Dispose destroys only the handles it created, never this one, so
            // there is no double-free even though the handle is parked in
            // poolB's free-list.
            AhjoValidation.Enabled = false;
            poolB.Release(foreign); // no throw
        }
        finally
        {
            AhjoValidation.Enabled = original;
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
