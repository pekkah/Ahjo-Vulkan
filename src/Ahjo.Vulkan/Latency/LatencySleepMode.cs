namespace Ahjo.Vulkan;

/// <summary>
/// The Reflex low-latency mode of a <see cref="Swapchain"/>, applied with
/// <see cref="Swapchain.SetLatencySleepMode"/> as a
/// <c>VkLatencySleepModeInfoNV</c> (issue #261).
/// </summary>
/// <param name="LowLatency"><c>lowLatencyMode</c>: Reflex Low Latency on.</param>
/// <param name="Boost"><c>lowLatencyBoost</c>: hint the driver to raise GPU
/// clocks at the cost of power. NVIDIA's integration guide requires
/// <paramref name="LowLatency"/> whenever Boost is set (Reflex guide l. 428),
/// but the Vulkan spec permits Boost alone (wsi l. 9179-9181), so the wrapper
/// passes the value through unchanged. Use <see cref="OnPlusBoost"/>.</param>
/// <param name="MinimumIntervalUs"><c>minimumIntervalUs</c>: a frame-rate cap
/// expressed as the minimum frame interval in microseconds; 0 means no
/// cap.</param>
public readonly record struct LatencySleepMode(bool LowLatency, bool Boost, uint MinimumIntervalUs)
{
    /// <summary>Reflex off: no low latency, no boost, no cap. Also
    /// <c>default</c>. Keep calling the per-frame members — NVIDIA requires
    /// markers and sleep regardless of mode.</summary>
    public static LatencySleepMode Off => default;

    /// <summary>Reflex Low Latency on, no boost, no cap — NVIDIA's required
    /// default.</summary>
    public static LatencySleepMode On => new(true, false, 0);

    /// <summary>Reflex Low Latency on plus Boost, no cap.</summary>
    public static LatencySleepMode OnPlusBoost => new(true, true, 0);
}
