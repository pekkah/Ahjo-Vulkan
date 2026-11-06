namespace Ahjo.Vulkan;

/// <summary>
/// A PC Latency (PCL) Stats marker, written by <see cref="PclStats.Marker"/>
/// as the <c>Marker</c> field of a <c>PCLStatsEvent</c> (issue #261).
/// </summary>
/// <remarks>
/// <para>The numbering is the PCL wire protocol's, as published under MIT in
/// NVIDIA Streamline's <c>include/sl_pcl.h:62-74</c> (tag <c>v2.14.16</c>) and
/// consumed by PresentMon. From 8 upward it differs from
/// <see cref="LatencyMarker"/> (<c>VkLatencyMarkerNV</c>):
/// <see cref="PcLatencyPing"/> takes 8, so the out-of-band present markers are
/// 11/12 here and 10/11 there. Never cast between the two enums.</para>
/// <para>The deprecated <c>INPUT_SAMPLE</c> (6) and the out-of-band
/// render-submit markers (9, 10) are omitted, for the reasons given on
/// <see cref="LatencyMarker"/>.</para>
/// </remarks>
public enum PclMarker : uint
{
    /// <summary>Start of simulation.</summary>
    SimulationStart = 0,

    /// <summary>End of simulation.</summary>
    SimulationEnd = 1,

    /// <summary>Start of render-command submission.</summary>
    RenderSubmitStart = 2,

    /// <summary>End of render-command submission.</summary>
    RenderSubmitEnd = 3,

    /// <summary>Before the application's logical present.</summary>
    PresentStart = 4,

    /// <summary>After the application's logical present.</summary>
    PresentEnd = 5,

    /// <summary>A click that should trigger the latency-flash
    /// indicator.</summary>
    TriggerFlash = 7,

    /// <summary>The application saw the PCL ping message. Emitted by
    /// <see cref="PclStats"/> itself ahead of the next
    /// <see cref="SimulationStart"/>; has no Vulkan counterpart.</summary>
    PcLatencyPing = 8,

    /// <summary>Before each present of a frame-generation path.</summary>
    OutOfBandPresentStart = 11,

    /// <summary>After each present of a frame-generation path.</summary>
    OutOfBandPresentEnd = 12,
}
