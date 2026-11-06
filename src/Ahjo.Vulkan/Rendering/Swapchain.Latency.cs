using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

// NVIDIA Reflex low latency over VK_NV_low_latency2 (issue #261). Every LL2
// call takes (device, swapchain), and Swapchain is the object that swaps its
// handle on Recreate, so the opt-in and the sleep mode live here and survive a
// resize (spec D3). The consumer contract is docs/reflex-notes.md.
public sealed unsafe partial class Swapchain
{
    // Construction-time opt-in (SwapchainDescription.LowLatency); Recreate
    // rejects a different value.
    private readonly bool              _lowLatency;
    // Last mode set; re-applied on every create.
    private LatencySleepMode           _sleepMode;
    // Wrapper-owned vkLatencySleepNV signal target, from _semaphorePool. Not
    // tied to the VkSwapchainKHR, so it survives Recreate; the pool destroys
    // it in Dispose. Default (null) when not opted in.
    private readonly TimelineSemaphore _sleepSemaphore;
    // Last value LatencySleep asked the driver to signal.
    private ulong                      _sleepValue;
    // KHR present-id high-water mark of the current VkSwapchainKHR; reset to
    // 0 on every create (VUID-VkPresentIdKHR-presentIds-04999).
    private ulong                      _lastPresentId;

    /// <summary>
    /// <see langword="true"/> when this swapchain was constructed with
    /// <see cref="SwapchainDescription.LowLatency"/>. Fixed for the swapchain's
    /// lifetime.
    /// </summary>
    public bool IsLowLatencyEnabled => _lowLatency;

    /// <summary>
    /// The mode last passed to <see cref="SetLatencySleepMode"/>;
    /// <see cref="Ahjo.Vulkan.LatencySleepMode.Off"/> until then. Re-applied on
    /// every swapchain create, so it survives <see cref="Recreate"/>.
    /// </summary>
    public LatencySleepMode LatencySleepMode => _sleepMode;

    /// <summary>
    /// Sets the Reflex low-latency mode (<c>vkSetLatencySleepModeNV</c>). This
    /// is the UI/setup call — it is "the only way" Reflex gets disabled
    /// (Reflex guide l. 393-394); the per-frame calls keep running in every
    /// mode.
    /// </summary>
    /// <remarks>
    /// <para>On a minimized swapchain with no handle the mode is stored and
    /// applied at the next create. Every create re-applies it.</para>
    /// <para><b>Threading.</b> May be called from a thread other than the one
    /// that presents (an exception to external synchronization, wsi
    /// l. 9151-9156), but must not overlap <see cref="Recreate"/> or
    /// <see cref="Dispose"/>.</para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The swapchain has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The swapchain was not
    /// created with <see cref="SwapchainDescription.LowLatency"/>. Hide Reflex
    /// controls when <see cref="Device.IsNvLowLatency2Enabled"/> is
    /// <see langword="false"/>.</exception>
    public void SetLatencySleepMode(in LatencySleepMode mode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_lowLatency)
            throw new InvalidOperationException(
                "SetLatencySleepMode requires a Swapchain created with SwapchainDescription.LowLatency = true. Hide Reflex controls when Device.IsNvLowLatency2Enabled is false.");
        _sleepMode = mode;
        ApplySleepMode();
    }

    /// <summary>
    /// The Reflex sleep (<c>vkLatencySleepNV</c> on the wrapper-owned timeline
    /// semaphore, then a host wait on it): blocks until the driver says the
    /// next frame should start.
    /// </summary>
    /// <param name="timeout">Upper bound on the host wait. Pass a bounded value
    /// (for example 1 s) so a driver that never signals cannot hang the frame
    /// loop.</param>
    /// <returns><see cref="WaitState.Signaled"/> when the driver released the
    /// frame; <see cref="WaitState.Timeout"/> when <paramref name="timeout"/>
    /// elapsed first — continue the frame anyway.
    /// <see cref="WaitState.Signaled"/> immediately when this swapchain is not
    /// opted in or holds no handle (minimized), so one frame loop runs on every
    /// vendor.</returns>
    /// <remarks>
    /// Call exactly once per <b>rendered</b> frame — not once per present, even
    /// with frame generation — and before input sampling (wsi l. 9203-9206,
    /// Reflex guide l. 517). Keep calling it with Reflex off.
    /// </remarks>
    public WaitState LatencySleep(TimeSpan timeout)
    {
        VkSwapchainKHR_T* h = _handle;
        if (!_lowLatency || h == null) return WaitState.Signaled;

        ulong v = ++_sleepValue;
        var info = new VkLatencySleepInfoNV
        {
            sType           = VkStructureType.VK_STRUCTURE_TYPE_LATENCY_SLEEP_INFO_NV,
            signalSemaphore = _sleepSemaphore.Handle,
            value           = v,
        };
        _device.Functions.LatencySleep(_device.Handle, h, &info).ThrowIfFailed();
        return _sleepSemaphore.WaitFor(v, timeout);
    }

    /// <summary>
    /// Sends a latency marker for rendered frame <paramref name="frameId"/>:
    /// <c>vkSetLatencyMarkerNV</c> when this swapchain is opted in, and the
    /// matching <see cref="PclStats"/> marker (a no-op until
    /// <see cref="PclStats.Initialize"/>), so one call per marker site covers
    /// both protocols (spec D7).
    /// </summary>
    /// <param name="marker">The marker.</param>
    /// <param name="frameId">The application's id for the rendered frame,
    /// starting at 1. One id per rendered frame, shared by every marker of
    /// that frame (including both out-of-band present pairs of a
    /// frame-generation frame) and by
    /// <c>DlssFrameGenerationInputs.BackbufferFrameId</c>. Must be unique
    /// within the last 1000 frames. Not a KHR present id.</param>
    /// <remarks>
    /// <para><b>Single present</b>: SimulationStart, SimulationEnd,
    /// RenderSubmitStart, PresentStart, present, PresentEnd,
    /// RenderSubmitEnd.</para>
    /// <para><b>Frame generation</b> (two presents per rendered frame): keep
    /// PresentStart/PresentEnd around the logical present and surround
    /// <b>each</b> <c>vkQueuePresentKHR</c> with
    /// <see cref="LatencyMarker.OutOfBandPresentStart"/> /
    /// <see cref="LatencyMarker.OutOfBandPresentEnd"/>, all with the same
    /// <paramref name="frameId"/> (wsi l. 9335-9343). The full sequence is in
    /// <c>docs/reflex-notes.md</c>.</para>
    /// <para><b>One exception to the PCL forward.</b> On an opted-in swapchain
    /// <see cref="LatencyMarker.OutOfBandPresentStart"/> /
    /// <see cref="LatencyMarker.OutOfBandPresentEnd"/> go to the driver only:
    /// the NVIDIA driver writes PCL 11/12 itself around the real OS present
    /// (measured, spec E6). Without the opt-in they are forwarded.</para>
    /// <para><b>Threading.</b> Markers may come from a thread other than the
    /// one that presents (wsi l. 9253-9258), but must not overlap
    /// <see cref="Recreate"/> or <see cref="Dispose"/>. PRESENT markers go on
    /// the presenting thread (Reflex guide l. 1323).</para>
    /// <para>Send markers with Reflex off and on other vendors too: NVIDIA
    /// requires them regardless of mode, and the PCL half works on every
    /// GPU.</para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frameId"/>
    /// is 0.</exception>
    public void SetLatencyMarker(LatencyMarker marker, ulong frameId)
    {
        ArgumentOutOfRangeException.ThrowIfZero(frameId);
        VkSwapchainKHR_T* h = _handle;
        if (_lowLatency && h != null)
        {
            var info = new VkSetLatencyMarkerInfoNV
            {
                sType     = VkStructureType.VK_STRUCTURE_TYPE_SET_LATENCY_MARKER_INFO_NV,
                presentID = frameId,
                marker    = LatencyMarkerMap.ToVk(marker),
            };
            _device.Functions.SetLatencyMarker(_device.Handle, h, &info);
        }
        // On an opted-in swapchain the driver writes the PCL out-of-band
        // present markers itself (spec E6/D7), so those two go to the driver
        // only.
        if (LatencyMarkerMap.ForwardsToPcl(marker, _lowLatency))
            PclStats.Marker(LatencyMarkerMap.ToPcl(marker), frameId);
    }

    /// <summary>
    /// Copies the driver's recent per-frame latency reports
    /// (<c>vkGetLatencyTimingsNV</c>) into <paramref name="destination"/>,
    /// oldest first (wsi l. 9384-9386). No copy and no allocation: the span is
    /// handed to the driver as <c>VkLatencyTimingsFrameReportNV*</c>.
    /// </summary>
    /// <param name="destination">Receives up to its length in reports. A stack
    /// span works.</param>
    /// <returns>The number of reports written; 0 when this swapchain is not
    /// opted in, holds no handle, or <paramref name="destination"/> is
    /// empty.</returns>
    public int GetLatencyTimings(scoped Span<LatencyTimings> destination)
    {
        VkSwapchainKHR_T* h = _handle;
        if (!_lowLatency || h == null || destination.IsEmpty) return 0;

        fixed (LatencyTimings* pw = destination)
        {
            var p = (VkLatencyTimingsFrameReportNV*)pw;
            for (int i = 0; i < destination.Length; i++)
            {
                p[i].sType = VkStructureType.VK_STRUCTURE_TYPE_LATENCY_TIMINGS_FRAME_REPORT_NV;
                p[i].pNext = null;
            }
            var info = new VkGetLatencyMarkerInfoNV
            {
                sType       = VkStructureType.VK_STRUCTURE_TYPE_GET_LATENCY_MARKER_INFO_NV,
                timingCount = (uint)destination.Length,
                pTimings    = p,
            };
            _device.Functions.GetLatencyTimings(_device.Handle, h, &info);
            return (int)info.timingCount;
        }
    }

    /// <summary>
    /// The maximum number of reports <see cref="GetLatencyTimings"/> can return
    /// (the NULL-<c>pTimings</c> query, wsi l. 9380-9381); 0 when this
    /// swapchain is not opted in or holds no handle.
    /// </summary>
    public int LatencyTimingCapacity
    {
        get
        {
            VkSwapchainKHR_T* h = _handle;
            if (!_lowLatency || h == null) return 0;
            var info = new VkGetLatencyMarkerInfoNV
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_GET_LATENCY_MARKER_INFO_NV,
            };
            _device.Functions.GetLatencyTimings(_device.Handle, h, &info);
            return (int)info.timingCount;
        }
    }

    // Re-applies the stored sleep mode after a create that fully succeeded
    // (constructor and Recreate). The spec is silent on whether the mode
    // survives oldSwapchain, so the re-apply is defensive (#261 spec D3) — and
    // it is the last step, outside CreateOrRecreate, so a failure here cannot
    // unwind a good swapchain. It is reported through AhjoDiagnostics, not
    // thrown: the swapchain stays valid and presentable, and the caller can
    // retry with SetLatencySleepMode, which does throw.
    private void ReapplySleepModeAfterCreate()
    {
        if (!_lowLatency || _state != SwapchainState.Ready) return;
        try
        {
            if (_failSleepModeApplyForTesting)
                throw new VulkanException(VkResult.VK_ERROR_INITIALIZATION_FAILED, "vkSetLatencySleepModeNV (test seam)");
            ApplySleepMode();
        }
        catch (VulkanException e)
        {
            AhjoDiagnostics.Write(DiagnosticSeverity.Warning, "Swapchain",
                "Re-applying the Reflex sleep mode after swapchain create failed (" + e.Result +
                "); the swapchain is valid but its sleep mode may be the driver default until SetLatencySleepMode is called again.");
        }
    }

    // Test seam (InternalsVisibleTo): make the next post-create re-apply fail.
    private bool _failSleepModeApplyForTesting;
    internal void FailSleepModeApplyForTesting(bool fail) => _failSleepModeApplyForTesting = fail;

    // Waits (bounded) for the last sleep signal the driver was asked to make,
    // so a pending vkLatencySleepNV signal cannot outlive the semaphore that
    // _semaphorePool.Dispose destroys. Called from Dispose after the swapchain
    // handle is destroyed.
    private void DrainPendingSleepSignal()
    {
        if (!_lowLatency || _sleepValue == 0) return;
        // Teardown must reach _semaphorePool.Dispose: an OOM from
        // vkWaitSemaphores here would otherwise leak every pooled semaphore.
        try { _ = _sleepSemaphore.WaitFor(_sleepValue, TimeSpan.FromMilliseconds(100)); }
        catch (VulkanException) { }
    }

    // Pushes _sleepMode to the current handle. No handle (minimized) → the
    // next create re-applies it.
    private void ApplySleepMode()
    {
        VkSwapchainKHR_T* h = _handle;
        if (h == null) return;
        LatencySleepMode m = _sleepMode;
        var info = new VkLatencySleepModeInfoNV
        {
            sType             = VkStructureType.VK_STRUCTURE_TYPE_LATENCY_SLEEP_MODE_INFO_NV,
            lowLatencyMode    = m.LowLatency ? 1u : 0u,
            lowLatencyBoost   = m.Boost ? 1u : 0u,
            minimumIntervalUs = m.MinimumIntervalUs,
        };
        _device.Functions.SetLatencySleepMode(_device.Handle, h, &info).ThrowIfFailed();
    }
}
