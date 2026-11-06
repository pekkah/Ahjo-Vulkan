using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Maps a <see cref="LatencyMarker"/> onto the two wire numberings it feeds
/// (issue #261 spec D7).
/// </summary>
internal static class LatencyMarkerMap
{
    /// <summary>
    /// The PCL Stats value for <paramref name="m"/>. An explicit table, not a
    /// cast: the PCL numbering is one higher from 8 upward.
    /// </summary>
    public static PclMarker ToPcl(LatencyMarker m) => m switch
    {
        LatencyMarker.SimulationStart       => PclMarker.SimulationStart,
        LatencyMarker.SimulationEnd         => PclMarker.SimulationEnd,
        LatencyMarker.RenderSubmitStart     => PclMarker.RenderSubmitStart,
        LatencyMarker.RenderSubmitEnd       => PclMarker.RenderSubmitEnd,
        LatencyMarker.PresentStart          => PclMarker.PresentStart,
        LatencyMarker.PresentEnd            => PclMarker.PresentEnd,
        LatencyMarker.TriggerFlash          => PclMarker.TriggerFlash,
        LatencyMarker.OutOfBandPresentStart => PclMarker.OutOfBandPresentStart,
        LatencyMarker.OutOfBandPresentEnd   => PclMarker.OutOfBandPresentEnd,
        _ => throw new ArgumentOutOfRangeException(nameof(m), m, "Undefined LatencyMarker value."),
    };

    /// <summary>
    /// The <c>VkLatencyMarkerNV</c> value for <paramref name="m"/>: a cast,
    /// because <see cref="LatencyMarker"/> uses the Vulkan numbering. The
    /// drift test <c>LatencyMarkerMapTests.ToVk_EqualsVkLatencyMarkerNV</c>
    /// pins every value to its named Vulkan constant.
    /// </summary>
    public static VkLatencyMarkerNV ToVk(LatencyMarker m) => (VkLatencyMarkerNV)(uint)m;

    /// <summary>
    /// Whether <see cref="Swapchain.SetLatencyMarker"/> forwards
    /// <paramref name="m"/> to <see cref="PclStats"/>. <see langword="false"/>
    /// exactly for <see cref="LatencyMarker.OutOfBandPresentStart"/> /
    /// <see cref="LatencyMarker.OutOfBandPresentEnd"/> on a low-latency
    /// swapchain: there the NVIDIA driver writes PCL 11/12 itself around the
    /// real OS present (measured, spec E6/D7), and an application copy would
    /// be stamped early. Without the opt-in no driver writes them, so they are
    /// forwarded.
    /// </summary>
    public static bool ForwardsToPcl(LatencyMarker m, bool lowLatencySwapchain)
        => !(lowLatencySwapchain
             && m is LatencyMarker.OutOfBandPresentStart or LatencyMarker.OutOfBandPresentEnd);
}
