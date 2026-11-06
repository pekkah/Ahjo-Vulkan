using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One frame's latency report from <see cref="Swapchain.GetLatencyTimings"/>,
/// a read-only view of <c>VkLatencyTimingsFrameReportNV</c> (issue #261). All
/// times are microseconds on the driver's clock; a timestamp the driver did
/// not record reads 0.
/// </summary>
/// <remarks>
/// The layout is identical to the native struct by construction — the single
/// field <b>is</b> the native struct — which is what lets
/// <see cref="Swapchain.GetLatencyTimings"/> hand the caller's span straight to
/// <c>vkGetLatencyTimingsNV</c> with no copy and no allocation.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly struct LatencyTimings
{
    private readonly VkLatencyTimingsFrameReportNV _native;

    /// <summary>The application frame id passed to
    /// <see cref="Swapchain.SetLatencyMarker"/> (<c>presentID</c>). Not a KHR
    /// present id. 0 means the slot is unpopulated.</summary>
    public ulong FrameId => _native.presentID;

    /// <summary><c>inputSampleTimeUs</c>.</summary>
    public ulong InputSampleTimeUs => _native.inputSampleTimeUs;

    /// <summary><c>simStartTimeUs</c>.</summary>
    public ulong SimulationStartTimeUs => _native.simStartTimeUs;

    /// <summary><c>simEndTimeUs</c>.</summary>
    public ulong SimulationEndTimeUs => _native.simEndTimeUs;

    /// <summary><c>renderSubmitStartTimeUs</c>.</summary>
    public ulong RenderSubmitStartTimeUs => _native.renderSubmitStartTimeUs;

    /// <summary><c>renderSubmitEndTimeUs</c>.</summary>
    public ulong RenderSubmitEndTimeUs => _native.renderSubmitEndTimeUs;

    /// <summary><c>presentStartTimeUs</c>.</summary>
    public ulong PresentStartTimeUs => _native.presentStartTimeUs;

    /// <summary><c>presentEndTimeUs</c>.</summary>
    public ulong PresentEndTimeUs => _native.presentEndTimeUs;

    /// <summary><c>driverStartTimeUs</c>.</summary>
    public ulong DriverStartTimeUs => _native.driverStartTimeUs;

    /// <summary><c>driverEndTimeUs</c>.</summary>
    public ulong DriverEndTimeUs => _native.driverEndTimeUs;

    /// <summary><c>osRenderQueueStartTimeUs</c>.</summary>
    public ulong OsRenderQueueStartTimeUs => _native.osRenderQueueStartTimeUs;

    /// <summary><c>osRenderQueueEndTimeUs</c>.</summary>
    public ulong OsRenderQueueEndTimeUs => _native.osRenderQueueEndTimeUs;

    /// <summary><c>gpuRenderStartTimeUs</c>.</summary>
    public ulong GpuRenderStartTimeUs => _native.gpuRenderStartTimeUs;

    /// <summary><c>gpuRenderEndTimeUs</c>.</summary>
    public ulong GpuRenderEndTimeUs => _native.gpuRenderEndTimeUs;
}
