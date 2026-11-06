namespace Ahjo.Vulkan;

/// <summary>
/// A Reflex latency marker, passed to <see cref="Swapchain.SetLatencyMarker"/>
/// (issue #261).
/// </summary>
/// <remarks>
/// <para><b>Numbering.</b> The values are <c>VkLatencyMarkerNV</c>'s, so the
/// Vulkan call is a cast. They are <b>not</b> <see cref="PclMarker"/>'s: from 8
/// upward PCL Stats numbers its markers one higher (PCL inserts
/// <c>PC_LATENCY_PING = 8</c>), so <see cref="OutOfBandPresentStart"/> is 10
/// here and 11 in PCL. <see cref="Swapchain.SetLatencyMarker"/> forwards every
/// marker to <see cref="PclStats"/> through an explicit table, never a
/// cast.</para>
/// <para><b>Absent values.</b> <c>INPUT_SAMPLE</c> (6) is omitted because the
/// Reflex SDK guide deprecates it. <c>OUT_OF_BAND_RENDERSUBMIT_START/END</c>
/// (8, 9) are omitted because they only mean something on an out-of-band queue
/// (<c>vkQueueNotifyOutOfBandNV</c>), which the wrapper does not expose. Both
/// are additive later.</para>
/// <para>The frame-loop order, including the frame-generation two-present
/// sequence, is in <c>docs/reflex-notes.md</c>.</para>
/// </remarks>
public enum LatencyMarker : uint
{
    /// <summary>Start of simulation for the frame (before input sampling's
    /// consumer).</summary>
    SimulationStart = 0,

    /// <summary>End of simulation.</summary>
    SimulationEnd = 1,

    /// <summary>Start of render-command submission.</summary>
    RenderSubmitStart = 2,

    /// <summary>End of render-command submission (after
    /// <see cref="PresentEnd"/> is the guide's typical order).</summary>
    RenderSubmitEnd = 3,

    /// <summary>Immediately before the application's logical present. Must be
    /// sent from the presenting thread.</summary>
    PresentStart = 4,

    /// <summary>Immediately after the application's logical present. Must be
    /// sent from the presenting thread.</summary>
    PresentEnd = 5,

    /// <summary>A click that should trigger the latency-flash indicator (for
    /// example, the left mouse button).</summary>
    TriggerFlash = 7,

    /// <summary>Immediately before each <c>vkQueuePresentKHR</c> a
    /// frame-generation path issues for one rendered frame.</summary>
    OutOfBandPresentStart = 10,

    /// <summary>Immediately after each <c>vkQueuePresentKHR</c> a
    /// frame-generation path issues for one rendered frame.</summary>
    OutOfBandPresentEnd = 11,
}
