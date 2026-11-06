using System.Diagnostics;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Issue #261: Reflex low latency over <c>VK_NV_low_latency2</c> on
/// <see cref="Swapchain"/>, and the KHR present-id overloads. Win32 + a
/// driver; the low-latency cases additionally need a GPU that advertises
/// <c>VK_NV_low_latency2</c> (NVIDIA), and skip <c>[gate:feature]</c>
/// elsewhere. Serialized with the PCL tests because
/// <see cref="Swapchain.SetLatencyMarker"/> forwards to the static
/// <see cref="PclStats"/>.
/// </summary>
[Collection(PclStatsCollection.Name)]
public sealed unsafe class SwapchainLowLatencyTests
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static void RequireWin32Driver()
    {
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();
    }

    // ---- Device without the extension ----

    [Fact]
    public void LowLatency_OnDeviceWithoutExtension_Throws()
    {
        RequireWin32Driver();
        using var host = Host.Create(lowLatency: false, presentIdFeature: false);
        // SwapchainDescription is a ref struct, so it cannot be captured by an
        // Assert.Throws lambda; build it inside.
        Host h = host;
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            var desc = h.Description(lowLatency: true);
            using var swap = new Swapchain(h.Device, in desc);
        });
        Assert.Contains("VK_NV_low_latency2", ex.Message);
        Assert.False(host.Device.IsNvLowLatency2Enabled);
        Assert.False(host.Device.IsPresentIdEnabled);
    }

    [Fact]
    public void NotOptedIn_PerFrameCallsDegrade()
    {
        RequireWin32Driver();
        using var host = Host.Create(lowLatency: false, presentIdFeature: false);
        var desc = host.Description(lowLatency: false);
        using var swap = new Swapchain(host.Device, in desc);

        Assert.False(swap.IsLowLatencyEnabled);
        var sw = Stopwatch.StartNew();
        Assert.Equal(WaitState.Signaled, swap.LatencySleep(TimeSpan.FromSeconds(1)));
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(1), $"LatencySleep took {sw.Elapsed.TotalMilliseconds} ms.");
        swap.SetLatencyMarker(LatencyMarker.SimulationStart, 1);
        swap.SetLatencyMarker(LatencyMarker.OutOfBandPresentEnd, 1);
        Assert.Equal(0, swap.GetLatencyTimings(stackalloc LatencyTimings[8]));
        Assert.Equal(0, swap.LatencyTimingCapacity);
        Assert.Throws<InvalidOperationException>(() => swap.SetLatencySleepMode(LatencySleepMode.On));
    }

    [Fact]
    public void NotOptedIn_PerFrameCalls_AreZeroAllocation()
    {
        RequireWin32Driver();
        using var host = Host.Create(lowLatency: false, presentIdFeature: false);
        var desc = host.Description(lowLatency: false);
        using var swap = new Swapchain(host.Device, in desc);

        static void Frame(Swapchain s, ulong id)
        {
            s.LatencySleep(TimeSpan.FromSeconds(1));
            s.SetLatencyMarker(LatencyMarker.SimulationStart, id);
            s.SetLatencyMarker(LatencyMarker.OutOfBandPresentEnd, id);
            s.GetLatencyTimings(stackalloc LatencyTimings[8]);
        }

        for (ulong id = 1; id <= 16; id++) Frame(swap, id);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (ulong id = 17; id < 17 + 128; id++) Frame(swap, id);
        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(0, after - before);
    }

    [Fact]
    public void FrameIdZero_Throws()
    {
        RequireWin32Driver();
        using var host = Host.Create(lowLatency: false, presentIdFeature: false);
        var desc = host.Description(lowLatency: false);
        using var swap = new Swapchain(host.Device, in desc);
        Assert.Throws<ArgumentOutOfRangeException>(() => swap.SetLatencyMarker(LatencyMarker.SimulationStart, 0));
    }

    [Fact]
    public void PresentId_WithoutExtension_Throws()
    {
        RequireWin32Driver();
        using var host = Host.Create(lowLatency: false, presentIdFeature: false);
        var desc = host.Description(lowLatency: false);
        using var swap = new Swapchain(host.Device, in desc);
        using var ring = host.Ring();

        using var fc = ring.BeginFrame();
        uint i = host.AcquireAndSubmit(swap, fc);
        Assert.Throws<InvalidOperationException>(() => swap.Present(host.Queue, i, 1UL));
        // The validation rejected the id before any present; finish the frame
        // so teardown stays clean.
        swap.Present(host.Queue, i, 0UL);
        host.Device.WaitIdle();
    }

    [Fact]
    public void PresentId_Zero_IsPlainPresent()
    {
        RequireWin32Driver();
        using var host = Host.Create(lowLatency: false, presentIdFeature: false);
        var desc = host.Description(lowLatency: false);
        using var swap = new Swapchain(host.Device, in desc);
        using var ring = host.Ring();

        for (int f = 0; f < 3; f++)
        {
            using var fc = ring.BeginFrame();
            uint i = host.AcquireAndSubmit(swap, fc);
            var r = swap.Present(host.Queue, i, 0UL);
            Assert.True(r is AcquireResult.Success or AcquireResult.Suboptimal, $"Present returned {r}.");
        }
        host.Device.WaitIdle();
    }

    // ---- VK_NV_low_latency2 ----

    [Fact]
    public void OptedIn_SleepMarkersTimings()
    {
        RequireWin32Driver();
        using var host = Host.Create(lowLatency: true, presentIdFeature: false);
        var desc = host.Description(lowLatency: true);
        using var swap = new Swapchain(host.Device, in desc);
        using var ring = host.Ring();

        Assert.True(host.Device.IsNvLowLatency2Enabled);
        Assert.True(host.Device.IsPresentIdEnabled);
        Assert.True(swap.IsLowLatencyEnabled);
        swap.SetLatencySleepMode(LatencySleepMode.On);
        Assert.Equal(LatencySleepMode.On, swap.LatencySleepMode);

        const ulong Frames = 120;
        for (ulong id = 1; id <= Frames; id++)
        {
            Assert.Equal(WaitState.Signaled, swap.LatencySleep(TimeSpan.FromSeconds(1)));
            swap.SetLatencyMarker(LatencyMarker.SimulationStart, id);
            swap.SetLatencyMarker(LatencyMarker.SimulationEnd, id);
            swap.SetLatencyMarker(LatencyMarker.RenderSubmitStart, id);
            using var fc = ring.BeginFrame();
            uint i = host.AcquireAndSubmit(swap, fc);
            swap.SetLatencyMarker(LatencyMarker.PresentStart, id);
            swap.Present(host.Queue, i);
            swap.SetLatencyMarker(LatencyMarker.PresentEnd, id);
            swap.SetLatencyMarker(LatencyMarker.RenderSubmitEnd, id);
        }
        host.Device.WaitIdle();

        Assert.True(swap.LatencyTimingCapacity >= 1, $"LatencyTimingCapacity = {swap.LatencyTimingCapacity}.");

        Span<LatencyTimings> timings = stackalloc LatencyTimings[64];
        int n = swap.GetLatencyTimings(timings);
        Assert.True(n > 0, "GetLatencyTimings returned no reports after 120 frames.");
        int populated = 0;
        for (int k = 0; k < n; k++)
        {
            ref readonly LatencyTimings t = ref timings[k];
            if (t.FrameId == 0) continue;
            populated++;
            Assert.InRange(t.FrameId, 1ul, Frames);
            if (t.SimulationStartTimeUs != 0 && t.SimulationEndTimeUs != 0)
                Assert.True(t.SimulationStartTimeUs <= t.SimulationEndTimeUs,
                    $"Frame {t.FrameId}: sim start {t.SimulationStartTimeUs} > sim end {t.SimulationEndTimeUs}.");
        }
        Assert.True(populated > 0, $"None of the {n} reports carried a frame id.");
    }

    [Fact]
    public void Recreate_KeepsOptInAndMode()
    {
        RequireWin32Driver();
        using var host = Host.Create(lowLatency: true, presentIdFeature: false);
        var desc = host.Description(lowLatency: true);
        using var swap = new Swapchain(host.Device, in desc);
        swap.SetLatencySleepMode(LatencySleepMode.OnPlusBoost);

        host.Window.Resize(1024, 768);
        var resized = host.Description(lowLatency: true);
        Assert.Equal(SwapchainState.Ready, swap.Recreate(in resized));

        Assert.True(swap.IsLowLatencyEnabled);
        Assert.Equal(LatencySleepMode.OnPlusBoost, swap.LatencySleepMode);
        Assert.Equal(WaitState.Signaled, swap.LatencySleep(TimeSpan.FromSeconds(1)));
    }

    /// <summary>
    /// Vulkan review of #261: a failed sleep-mode re-apply after a good create
    /// must not unwind the swapchain. It is reported through
    /// <see cref="AhjoDiagnostics.Sink"/> and the swapchain stays Ready and
    /// presentable.
    /// </summary>
    [Fact]
    public void Recreate_SleepModeReapplyFailure_KeepsSwapchainAndReports()
    {
        RequireWin32Driver();
        using var host = Host.Create(lowLatency: true, presentIdFeature: false);
        var desc = host.Description(lowLatency: true);
        using var swap = new Swapchain(host.Device, in desc);
        using var ring = host.Ring();
        swap.SetLatencySleepMode(LatencySleepMode.On);

        var messages = new List<string>();
        DiagnosticSink previous = AhjoDiagnostics.Sink;
        AhjoDiagnostics.Sink = (severity, source, message) =>
        {
            if (source == "Swapchain") lock (messages) messages.Add(message);
        };
        SwapchainState state;
        try
        {
            swap.FailSleepModeApplyForTesting(true);
            host.Window.Resize(1024, 768);
            var resized = host.Description(lowLatency: true);
            state = swap.Recreate(in resized);
        }
        finally
        {
            swap.FailSleepModeApplyForTesting(false);
            AhjoDiagnostics.Sink = previous;
        }

        Assert.Equal(SwapchainState.Ready, state);
        Assert.Contains(messages, m => m.Contains("Reflex sleep mode", StringComparison.Ordinal));
        Assert.True(swap.IsLowLatencyEnabled);
        Assert.Equal(LatencySleepMode.On, swap.LatencySleepMode);

        // Still a working swapchain: sleep, present, and the mode can be set again.
        ring.RecycleStaleAcquireSemaphores();
        Assert.Equal(WaitState.Signaled, swap.LatencySleep(TimeSpan.FromSeconds(1)));
        using (var fc = ring.BeginFrame())
        {
            uint i = host.AcquireAndSubmit(swap, fc);
            var r = swap.Present(host.Queue, i);
            Assert.True(r is AcquireResult.Success or AcquireResult.Suboptimal, $"Present returned {r}.");
        }
        swap.SetLatencySleepMode(LatencySleepMode.On);
        host.Device.WaitIdle();
    }

    [Fact]
    public void Recreate_WithDifferentLowLatency_Throws()
    {
        RequireWin32Driver();
        using var host = Host.Create(lowLatency: true, presentIdFeature: false);
        var desc = host.Description(lowLatency: true);
        using var swap = new Swapchain(host.Device, in desc);
        Host h = host;
        Assert.Throws<ArgumentException>(() =>
        {
            var other = h.Description(lowLatency: false);
            swap.Recreate(in other);
        });
        Assert.True(swap.IsLowLatencyEnabled);
    }

    [Fact]
    public void PresentId_StrictlyIncreasing()
    {
        RequireWin32Driver();
        using var host = Host.Create(lowLatency: true, presentIdFeature: true);
        var desc = host.Description(lowLatency: true);
        using var swap = new Swapchain(host.Device, in desc);
        using var ring = host.Ring();

        PresentOne(host, swap, ring, 1);
        PresentOne(host, swap, ring, 2);

        using (var fc = ring.BeginFrame())
        {
            uint i = host.AcquireAndSubmit(swap, fc);
            Assert.Throws<ArgumentOutOfRangeException>(() => swap.Present(host.Queue, i, 2UL));
            swap.Present(host.Queue, i, 3UL);
        }

        // A new VkSwapchainKHR restarts the sequence.
        host.Device.WaitIdle();
        host.Window.Resize(900, 700);
        var resized = host.Description(lowLatency: true);
        Assert.Equal(SwapchainState.Ready, swap.Recreate(in resized));
        ring.RecycleStaleAcquireSemaphores();
        PresentOne(host, swap, ring, 1);
        host.Device.WaitIdle();
    }

    private static void PresentOne(Host host, Swapchain swap, FrameRing ring, ulong presentId)
    {
        using var fc = ring.BeginFrame();
        uint i = host.AcquireAndSubmit(swap, fc);
        var r = swap.Present(host.Queue, i, presentId);
        Assert.True(r is AcquireResult.Success or AcquireResult.Suboptimal, $"Present({presentId}) returned {r}.");
    }

    // ---- Fixture ----

    internal sealed class Host : IDisposable
    {
        public Win32Window Window   { get; }
        public Instance    Instance { get; }
        public Surface     Surface  { get; }
        public Device      Device   { get; }
        public uint        Family   { get; }
        public Queue       Queue    { get; }

        private Host(Win32Window window, Instance instance, Surface surface, Device device, uint family)
        {
            Window = window; Instance = instance; Surface = surface; Device = device; Family = family;
            Queue = device.GetQueue(family, 0);
        }

        /// <summary>
        /// <paramref name="lowLatency"/> true picks a GPU advertising
        /// <c>VK_NV_low_latency2</c> (gating <c>[gate:feature]</c> when none
        /// does) and enables it with <c>VK_KHR_present_id</c>;
        /// <paramref name="presentIdFeature"/> also pushes the
        /// <c>presentId</c> feature.
        /// </summary>
        public static Host Create(bool lowLatency, bool presentIdFeature, bool validation = false,
            Action<DebugMessage>? debugCallback = null)
        {
            var window = new Win32Window(800, 600, $"AhjoVk_LL_{Guid.NewGuid():N}");
            Instance? instance = null;
            Surface surface = default;
            try
            {
                Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
                instance = Instance.Create(new InstanceDescription
                {
                    Extensions       = instanceExts,
                    EnableValidation = validation,
                    DebugCallback    = debugCallback,
                });
                surface = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);

                Surface local  = surface;
                uint    chosen = uint.MaxValue;
                bool    any    = false;
                PhysicalDevice? gpu = null;
                try
                {
                    gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
                    {
                        if (lowLatency && !info.Device.SupportsExtension(VulkanExtensions.NvLowLatency2))
                            return false;
                        for (int i = 0; i < info.QueueFamilies.Length; i++)
                        {
                            if (!info.QueueFamilies[i].SupportsGraphics) continue;
                            if (info.Device.SupportsPresent(info.QueueFamilies[i].Index, in local))
                            {
                                chosen = info.QueueFamilies[i].Index;
                                return true;
                            }
                        }
                        return false;
                    });
                    any = true;
                }
                catch (VulkanException) when (lowLatency)
                {
                }
                TestGate.RequireDeviceFeature(any, "VK_NV_low_latency2 not advertised.");

                Utf8Name[] deviceExts = lowLatency
                    ? [VulkanExtensions.KhrSwapchain, VulkanExtensions.NvLowLatency2, VulkanExtensions.KhrPresentId]
                    : [VulkanExtensions.KhrSwapchain];
                Device device = gpu!.CreateDevice(new DeviceDescription
                {
                    Queues            = [new QueueRequest(chosen, count: 1, priority: 1.0f)],
                    Extensions        = deviceExts,
                    ConfigureFeatures = presentIdFeature
                        ? static (
                            ref ChainBuilder<VkDeviceCreateInfo> chain,
                            ref VkPhysicalDeviceFeatures2        _,
                            ref VkPhysicalDeviceVulkan12Features _,
                            ref VkPhysicalDeviceVulkan13Features _,
                            ref VkPhysicalDeviceVulkan14Features _) =>
                        {
                            ref var pid = ref chain.Push<VkPhysicalDevicePresentIdFeaturesKHR>();
                            pid.presentId = 1;
                        }
                        : null,
                });
                return new Host(window, instance, surface, device, chosen);
            }
            catch
            {
                if (!surface.IsNull) surface.Dispose();
                instance?.Dispose();
                window.Dispose();
                throw;
            }
        }

        public SwapchainDescription Description(bool lowLatency) => new()
        {
            Surface    = Surface,
            Width      = Window.Width,
            Height     = Window.Height,
            LowLatency = lowLatency,
        };

        public FrameRing Ring() => new(Device, framesInFlight: 2, queueFamily: Family);

        /// <summary>Acquires an image and submits a barrier-only
        /// UNDEFINED → PRESENT_SRC transition that signals its
        /// RenderingDone.</summary>
        public uint AcquireAndSubmit(Swapchain swap, FrameContext fc)
        {
            var acq = swap.AcquireNextImage(fc.ImageAcquired, TimeSpan.FromSeconds(1), out uint imageIndex);
            Assert.True(acq is AcquireResult.Success or AcquireResult.Suboptimal, $"AcquireNextImage returned {acq}.");
            fc.MarkImageAcquireSignaled();
            var rec = fc.CommandBuffers.Begin();
            try
            {
                rec.PipelineBarrier(PresentBarrier(swap, imageIndex));
                fc.Submit(Queue, ref rec, swap, imageIndex);
            }
            finally { rec.Dispose(); }
            return imageIndex;
        }

        public static ImageBarrier PresentBarrier(Swapchain swap, uint imageIndex) => new()
        {
            Image          = swap.GetImageHandle(imageIndex),
            SrcStage       = Stage.ColorAttachmentOutput, SrcAccess = Access.None,
            DstStage       = Stage.BottomOfPipe,          DstAccess = Access.None,
            OldLayout      = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            NewLayout      = VkImageLayout.VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
            Aspect         = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            BaseMipLevel   = 0, LevelCount = 1,
            BaseArrayLayer = 0, LayerCount = 1,
        };

        public void Dispose()
        {
            Device.WaitIdle();
            Device.Dispose();
            Surface.Dispose();
            Instance.Dispose();
            Window.Dispose();
        }
    }
}
