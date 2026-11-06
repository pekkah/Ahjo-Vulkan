using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Tests;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Issue #261: the per-frame Reflex members — <see cref="Swapchain.SetLatencyMarker"/>,
/// <see cref="Swapchain.GetLatencyTimings"/>, <see cref="Swapchain.LatencySleep"/>
/// and the present-id <see cref="Swapchain.Present(Queue, uint, ulong)"/> —
/// must allocate nothing.
/// </summary>
/// <remarks>
/// <para>Hardware-only: an NVIDIA RTX GPU whose driver advertises
/// <c>VK_NV_low_latency2</c>, on Windows (a hidden Win32 window provides the
/// surface). <see cref="Setup"/> <b>throws</b> rather than skipping, for the
/// reason <see cref="DlssFrameGenerationEvaluateBenchmarks"/> gives: a skipped
/// row reads like a clean one.</para>
/// <para>The swapchain asks for <c>VK_PRESENT_MODE_IMMEDIATE_KHR</c> and falls
/// back to FIFO; <see cref="Setup"/> prints which one it got. Either way the
/// frame row is paced by the display (the sleep alone is one refresh, issue
/// #261 OPEN-7), so read it for <c>Allocated</c>, not for time.</para>
/// </remarks>
[MemoryDiagnoser]
public unsafe class LowLatencyBenchmarks
{
    private Win32Window? _window;
    private Instance     _instance = null!;
    private Surface      _surface;
    private Device       _device   = null!;
    private Swapchain    _swap     = null!;
    private FrameRing    _ring     = null!;
    private Queue        _queue    = null!;
    private uint         _family;

    private ulong _frameId;
    private ulong _presentId;

    [GlobalSetup(Targets = [
        nameof(SetLatencyMarker_FgFrame), nameof(GetLatencyTimings_64),
        nameof(Frame_SleepMarkersPresentWithId), nameof(Frame_PresentNoId), nameof(Frame_PresentSemaphoreWithId)])]
    public void Setup()
    {
        AhjoValidation.Enabled = false;
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("LowLatencyBenchmarks needs Windows (Win32 surface).");

        _window = new Win32Window(640, 480, $"AhjoVk_LLBench_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        _instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        _surface  = Surface.CreateWin32(_instance, _window.HInstance, _window.Hwnd);

        Surface local  = _surface;
        uint    chosen = uint.MaxValue;
        PhysicalDevice gpu;
        try
        {
            gpu = _instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
            {
                if (!info.Device.SupportsExtension(VulkanExtensions.NvLowLatency2)) return false;
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
        }
        catch (VulkanException e)
        {
            throw new InvalidOperationException(
                "LowLatencyBenchmarks needs a GPU that advertises VK_NV_low_latency2 (NVIDIA RTX).", e);
        }
        _family = chosen;

        Utf8Name[] deviceExts = [VulkanExtensions.KhrSwapchain, VulkanExtensions.NvLowLatency2, VulkanExtensions.KhrPresentId];
        _device = gpu.CreateDevice(new DeviceDescription
        {
            Queues            = [new QueueRequest(_family, count: 1, priority: 1.0f)],
            Extensions        = deviceExts,
            ConfigureFeatures = static (
                ref ChainBuilder<VkDeviceCreateInfo> chain,
                ref VkPhysicalDeviceFeatures2        _,
                ref VkPhysicalDeviceVulkan12Features _,
                ref VkPhysicalDeviceVulkan13Features _,
                ref VkPhysicalDeviceVulkan14Features _) =>
            {
                ref var pid = ref chain.Push<VkPhysicalDevicePresentIdFeaturesKHR>();
                pid.presentId = 1;
            },
        });

        var desc = new SwapchainDescription
        {
            Surface              = _surface,
            Width                = _window.Width,
            Height               = _window.Height,
            PreferredPresentMode = VkPresentModeKHR.VK_PRESENT_MODE_IMMEDIATE_KHR,
            LowLatency           = true,
        };
        _swap = new Swapchain(_device, in desc);
        _swap.SetLatencySleepMode(LatencySleepMode.On);
        Console.WriteLine($"// LowLatencyBenchmarks present mode: {_swap.PresentMode}");

        _ring  = new FrameRing(_device, framesInFlight: 2, queueFamily: _family);
        _queue = _device.GetQueue(_family, 0);

        // Warm every slot, the marker path and the timings path.
        for (int i = 0; i < 8; i++) Frame_SleepMarkersPresentWithId();
        SetLatencyMarker_FgFrame();
        GetLatencyTimings_64();
    }

    /// <summary>Setup for <see cref="SetLatencyMarker_FgFrame_PclListening"/>:
    /// the shared setup, then PCL Stats initialized with the listener forced on,
    /// so every forwarded marker takes the real <c>EventWriteTransfer</c>
    /// path.</summary>
    [GlobalSetup(Target = nameof(SetLatencyMarker_FgFrame_PclListening))]
    public void SetupPclListening()
    {
        Setup();
        PclStats.Initialize(new PclStatsOptions());
        if (!PclStats.IsInitialized)
            throw new InvalidOperationException("PclStats.Initialize did not register the provider.");
        PclStats.SetProviderEnabledForTesting(true);
        SetLatencyMarker_FgFrame_PclListening();
    }

    [GlobalCleanup(Target = nameof(SetLatencyMarker_FgFrame_PclListening))]
    public void CleanupPclListening()
    {
        PclStats.SetProviderEnabledForTesting(false);
        PclStats.Shutdown();
        Cleanup();
    }

    [GlobalCleanup(Targets = [
        nameof(SetLatencyMarker_FgFrame), nameof(GetLatencyTimings_64),
        nameof(Frame_SleepMarkersPresentWithId), nameof(Frame_PresentNoId), nameof(Frame_PresentSemaphoreWithId)])]
    public void Cleanup()
    {
        _device.WaitIdle();
        _ring.Dispose();
        _swap.Dispose();
        _device.Dispose();
        _surface.Dispose();
        _instance.Dispose();
        _window?.Dispose();
    }

    /// <summary>The ten spec-D5 frame-generation markers for one frame id.
    /// PCL is not initialized here, so the forwarded half is the early
    /// return.</summary>
    [Benchmark(OperationsPerInvoke = 10)]
    public void SetLatencyMarker_FgFrame() => FgFrameMarkers();

    /// <summary>
    /// The same ten markers with PCL Stats initialized and the listener forced
    /// on. On this low-latency swapchain the four out-of-band present markers
    /// go to the driver only (spec D7, <c>LatencyMarkerMap.ForwardsToPcl</c>),
    /// so six of the ten also take the real <c>EventWriteTransfer</c> path.
    /// </summary>
    [Benchmark(OperationsPerInvoke = 10)]
    public void SetLatencyMarker_FgFrame_PclListening() => FgFrameMarkers();

    private void FgFrameMarkers()
    {
        ulong n = ++_frameId;
        Swapchain s = _swap;
        s.SetLatencyMarker(LatencyMarker.SimulationStart, n);
        s.SetLatencyMarker(LatencyMarker.SimulationEnd, n);
        s.SetLatencyMarker(LatencyMarker.RenderSubmitStart, n);
        s.SetLatencyMarker(LatencyMarker.PresentStart, n);
        s.SetLatencyMarker(LatencyMarker.OutOfBandPresentStart, n);
        s.SetLatencyMarker(LatencyMarker.OutOfBandPresentEnd, n);
        s.SetLatencyMarker(LatencyMarker.OutOfBandPresentStart, n);
        s.SetLatencyMarker(LatencyMarker.OutOfBandPresentEnd, n);
        s.SetLatencyMarker(LatencyMarker.PresentEnd, n);
        s.SetLatencyMarker(LatencyMarker.RenderSubmitEnd, n);
    }

    [Benchmark]
    public int GetLatencyTimings_64()
        => _swap.GetLatencyTimings(stackalloc LatencyTimings[64]);

    /// <summary>Sleep + six single-present markers + BeginFrame + acquire +
    /// barrier-only record + submit + <c>Present(queue, i, presentId)</c>.</summary>
    [Benchmark]
    public AcquireResult Frame_SleepMarkersPresentWithId() => Frame(PresentKind.WithId);

    /// <summary>The same frame through the plain <c>Present(queue, i)</c>
    /// (no <c>pNext</c>) on a low-latency swapchain.</summary>
    [Benchmark]
    public AcquireResult Frame_PresentNoId() => Frame(PresentKind.NoId);

    /// <summary>The same frame through
    /// <c>Present(queue, i, in BinarySemaphore, presentId)</c>.</summary>
    [Benchmark]
    public AcquireResult Frame_PresentSemaphoreWithId() => Frame(PresentKind.SemaphoreWithId);

    private enum PresentKind { WithId, NoId, SemaphoreWithId }

    private AcquireResult Frame(PresentKind kind)
    {
        Swapchain s = _swap;
        ulong n = ++_frameId;
        s.LatencySleep(TimeSpan.FromSeconds(1));
        s.SetLatencyMarker(LatencyMarker.SimulationStart, n);
        s.SetLatencyMarker(LatencyMarker.SimulationEnd, n);
        s.SetLatencyMarker(LatencyMarker.RenderSubmitStart, n);

        using var fc = _ring.BeginFrame();
        var acq = s.AcquireNextImage(fc.ImageAcquired, TimeSpan.FromSeconds(1), out uint imageIndex);
        if (acq is not (AcquireResult.Success or AcquireResult.Suboptimal))
            throw new InvalidOperationException("AcquireNextImage failed in a benchmark.");
        fc.MarkImageAcquireSignaled();

        var rec = fc.CommandBuffers.Begin();
        try
        {
            rec.PipelineBarrier(new ImageBarrier
            {
                Image          = s.GetImageHandle(imageIndex),
                SrcStage       = Stage.ColorAttachmentOutput, SrcAccess = Access.None,
                DstStage       = Stage.BottomOfPipe,          DstAccess = Access.None,
                OldLayout      = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                NewLayout      = VkImageLayout.VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
                Aspect         = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                BaseMipLevel   = 0, LevelCount = 1,
                BaseArrayLayer = 0, LayerCount = 1,
            });
            fc.Submit(_queue, ref rec, s, imageIndex);
        }
        finally { rec.Dispose(); }

        s.SetLatencyMarker(LatencyMarker.PresentStart, n);
        AcquireResult r = kind switch
        {
            PresentKind.NoId => s.Present(_queue, imageIndex),
            PresentKind.SemaphoreWithId => s.Present(_queue, imageIndex, s.GetRenderingDoneFor(imageIndex), ++_presentId),
            _ => s.Present(_queue, imageIndex, ++_presentId),
        };
        s.SetLatencyMarker(LatencyMarker.PresentEnd, n);
        s.SetLatencyMarker(LatencyMarker.RenderSubmitEnd, n);
        return r;
    }
}
