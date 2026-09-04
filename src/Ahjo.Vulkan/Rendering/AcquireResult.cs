namespace Ahjo.Vulkan;

/// <summary>
/// Outcome of <see cref="Swapchain.AcquireNextImage"/>. Mirrors the
/// distinct <see cref="Native.VkResult"/> values that
/// <c>vkAcquireNextImageKHR</c> reports as informational rather than
/// hard failures, plus a synthetic <see cref="Timeout"/> for the
/// CPU-side wait expiring.
/// </summary>
/// <remarks>
/// <para>All six members are reachable from
/// <see cref="Swapchain.AcquireNextImage"/>; only the first four are
/// reachable from <see cref="Swapchain.Present(Queue, uint)"/> — see the
/// remarks there. Each carries an obligation. Most are advisory — only
/// <see cref="SurfaceLost"/> leaves the swapchain in a state the API-boundary
/// guard rejects, so a caller that treats <i>that</i> one as noise will hit
/// <see cref="System.InvalidOperationException"/> out of the next acquire or
/// present (#220, #222). Since #222 the state it lands in names its own cause,
/// so a frame loop can make the stop-or-recreate decision from
/// <c>Swapchain.State</c> alone:</para>
/// <list type="table">
///   <listheader>
///     <term>Result</term>
///     <term>Swapchain state after</term>
///     <term>What the caller must do</term>
///   </listheader>
///   <item>
///     <term><see cref="Success"/></term>
///     <term>untouched</term>
///     <term>Render the image.</term>
///   </item>
///   <item>
///     <term><see cref="Suboptimal"/></term>
///     <term>untouched</term>
///     <term>The frame is usable; call <see cref="Swapchain.Recreate"/> at the
///     next convenient point. Unlike every other non-<see cref="Success"/> row,
///     an image <i>was</i> acquired and the semaphore <i>was</i> signalled: if
///     you bail out to <see cref="Swapchain.Recreate"/> without submitting a
///     wait that consumes it, rotate the acquire semaphore
///     (<see cref="FrameRing.RecycleStaleAcquireSemaphores"/>) — Vulkan has no
///     host reset for a binary semaphore
///     (VUID-vkAcquireNextImageKHR-semaphore-01779).</term>
///   </item>
///   <item>
///     <term><see cref="OutOfDate"/></term>
///     <term><see cref="SwapchainState.NeedsRecreate"/></term>
///     <term>Call <see cref="Swapchain.Recreate"/>. The state is advisory:
///     acquire and present stay legal meanwhile and keep reporting this.</term>
///   </item>
///   <item>
///     <term><see cref="SurfaceLost"/></term>
///     <term><see cref="SwapchainState.SurfaceLost"/></term>
///     <term><b>Terminal.</b> Rebuild the <c>VkSurfaceKHR</c> as well, or stop.
///     <b>Do not retry over the same surface</b> — see the member docs. The
///     state alone is now enough to make that call at the top of a frame loop
///     (#222), so this no longer has to be handled at every acquire and present
///     site.</term>
///   </item>
///   <item>
///     <term><see cref="Timeout"/></term>
///     <term>untouched</term>
///     <term>Retry on the next iteration; there is nothing to clean up.</term>
///   </item>
///   <item>
///     <term><see cref="NotReady"/></term>
///     <term>untouched</term>
///     <term>Retry on the next iteration; there is nothing to clean up.</term>
///   </item>
/// </list>
/// <para>The per-state obligations themselves live on
/// <see cref="SwapchainState"/> (#120); this table only says which result
/// leads to which state.</para>
/// </remarks>
public enum AcquireResult
{
    /// <summary>Image is ready and the bound semaphore will signal.</summary>
    Success,
    /// <summary>Image acquired, but the swapchain no longer matches the
    /// surface optimally (rotation/HDR/etc). Frame is usable; recreate at
    /// the next convenient point.</summary>
    Suboptimal,
    /// <summary>Surface has changed (typically a resize). Caller must
    /// <see cref="Swapchain.Recreate"/> before any further acquires.</summary>
    OutOfDate,
    /// <summary>The platform <c>VkSurfaceKHR</c> the swapchain was built
    /// over is no longer valid (window destroyed, monitor unplugged, …).
    /// Recovery requires destroying the swapchain AND the surface, then
    /// re-creating both — a strict superset of the
    /// <see cref="OutOfDate"/> path. Distinguishing this from
    /// <see cref="OutOfDate"/> lets the caller branch on the heavier
    /// recreate without having to inspect the underlying
    /// <c>VkResult</c>.
    /// <para>The swapchain has <b>already</b> moved to
    /// <see cref="SwapchainState.SurfaceLost"/> by the time this is returned, and
    /// it is returned <i>without</i> throwing. A caller that merely
    /// <c>continue</c>s its frame loop will therefore get an
    /// <see cref="System.InvalidOperationException"/> out of the <i>next</i>
    /// <see cref="Swapchain.AcquireNextImage"/> or
    /// <see cref="Swapchain.Present(Queue, uint)"/>. A
    /// <see cref="Swapchain.Recreate"/> over the same <c>VkSurfaceKHR</c>
    /// cannot succeed, so this is terminal for that surface: rebuild the
    /// surface too, or stop (#220).</para></summary>
    SurfaceLost,
    /// <summary>CPU-side timeout elapsed without an image becoming
    /// available.
    /// <para>The swapchain state is untouched and the semaphore passed to
    /// <see cref="Swapchain.AcquireNextImage"/> was <i>not</i> signalled, so a
    /// bare retry on the next iteration is correct and there is no stale
    /// semaphore to rotate. Reachable from
    /// <see cref="Swapchain.AcquireNextImage"/> only — see the remarks on
    /// <see cref="Swapchain.Present(Queue, uint, in BinarySemaphore)"/>.</para></summary>
    Timeout,
    /// <summary>Non-blocking call (<c>timeout = 0</c>) found no image
    /// ready yet.
    /// <para>Like <see cref="Timeout"/>: the swapchain state is untouched, the
    /// acquire semaphore was not signalled, a bare retry is correct, and the
    /// member is reachable from <see cref="Swapchain.AcquireNextImage"/>
    /// only.</para></summary>
    NotReady,
}
