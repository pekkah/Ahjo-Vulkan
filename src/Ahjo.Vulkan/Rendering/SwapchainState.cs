namespace Ahjo.Vulkan;

/// <summary>
/// Lifecycle state of a <see cref="Swapchain"/> (issue #120). Makes the
/// "window minimized" and "recreate failed" situations legal states with
/// documented transitions instead of corrupted object state, and (issue
/// #222) splits the former single failure member into one recoverable and
/// two terminal causes. The invariant a caller needs:
/// <see cref="Ready"/> and <see cref="NeedsRecreate"/> are the only
/// presentable states; <see cref="Minimized"/> and
/// <see cref="RecreateFailed"/> are recovered by
/// <see cref="Swapchain.Recreate"/>; <see cref="SurfaceLost"/> and
/// <see cref="DeviceLost"/> are terminal and only
/// <see cref="Swapchain.Dispose"/> is legal.
/// <para>Whether a <c>VkSwapchainKHR</c> handle is still held is a property
/// of <i>where</i> a state was entered, not of the state itself: a swapchain
/// constructed over a zero-extent surface starts in <see cref="Minimized"/>
/// with nothing created. Callers never need to track this —
/// <see cref="Swapchain.Dispose"/> is legal from every state and destroys
/// whatever is still held.</para>
/// </summary>
public enum SwapchainState
{
    /// <summary>
    /// Swapchain exists and matches the surface;
    /// <see cref="Swapchain.AcquireNextImage"/> /
    /// <see cref="Swapchain.Present(Queue, uint)"/> are legal.
    /// </summary>
    Ready,

    /// <summary>
    /// Acquire or Present returned <see cref="AcquireResult.OutOfDate"/>.
    /// Acquire/present remain legal (they will keep reporting
    /// <c>OutOfDate</c>); call <see cref="Swapchain.Recreate"/>.
    /// <see cref="AcquireResult.Suboptimal"/> does <b>not</b> enter this
    /// state — the image is presentable per spec and recreating stays the
    /// caller's choice.
    /// </summary>
    NeedsRecreate,

    /// <summary>
    /// The surface currently has zero extent (minimized window — issue
    /// #110). There is no usable swapchain for this frame;
    /// <see cref="Swapchain.AcquireNextImage"/> and
    /// <see cref="Swapchain.Present(Queue, uint)"/> throw
    /// <see cref="InvalidOperationException"/>. Poll the window size and
    /// call <see cref="Swapchain.Recreate"/> when it is restored.
    /// </summary>
    Minimized,

    /// <summary>
    /// A <see cref="Swapchain.Recreate"/> threw after the old swapchain was
    /// retired (issue #112) for a reason other than the two terminal causes
    /// below. <b>No swapchain handle is held</b>, and
    /// <see cref="Swapchain.AcquireNextImage"/> /
    /// <see cref="Swapchain.Present(Queue, uint)"/> throw
    /// <see cref="InvalidOperationException"/>.
    /// <para><b>Retry is the documented recovery</b>: call
    /// <see cref="Swapchain.Recreate"/> again and it runs a from-scratch
    /// create with no <c>oldSwapchain</c> to pass.
    /// <see cref="Swapchain.Dispose"/> is always legal.</para>
    /// </summary>
    RecreateFailed,

    /// <summary>
    /// <c>VK_ERROR_SURFACE_LOST_KHR</c> was observed for the
    /// <see cref="Surface"/> this swapchain was built over (issue #222):
    /// either reported by <see cref="Swapchain.AcquireNextImage"/> /
    /// <see cref="Swapchain.Present(Queue, uint)"/> and returned as
    /// <see cref="AcquireResult.SurfaceLost"/> without throwing, or thrown
    /// out of <see cref="Swapchain.Recreate"/> — where it is a documented
    /// return code of every surface query and of <c>vkCreateSwapchainKHR</c>,
    /// and is in fact the likeliest place a real surface loss is first seen
    /// (driver restart → <see cref="AcquireResult.OutOfDate"/> → the caller
    /// calls <c>Recreate</c> → the capability query fails).
    /// <para><b>Terminal for this surface.</b>
    /// <see cref="Swapchain.Recreate"/> accepts only the
    /// <see cref="Surface"/> this swapchain was constructed with, and that
    /// surface is gone — so it throws <see cref="VulkanException"/> carrying
    /// <c>VK_ERROR_SURFACE_LOST_KHR</c> rather than pretending a
    /// same-surface retry could work.</para>
    /// <para>Recovery: dispose this swapchain, dispose and rebuild the
    /// <see cref="Surface"/>, then construct a new <see cref="Swapchain"/>.
    /// <see cref="Swapchain.Dispose"/> is legal here and destroys whatever is
    /// still held — which depends on where this state was entered, per the
    /// note on the enum.</para>
    /// </summary>
    SurfaceLost,

    /// <summary>
    /// The <c>VkDevice</c> was lost — observed by
    /// <see cref="Swapchain.Recreate"/>'s fast-fail on
    /// <see cref="Device.IsLost"/>, by <c>VK_ERROR_DEVICE_LOST</c> out of a
    /// call <see cref="Swapchain.Recreate"/> itself makes, or by the same
    /// code out of <see cref="Swapchain.AcquireNextImage"/> /
    /// <see cref="Swapchain.Present(Queue, uint)"/>. All three throw
    /// (issue #222).
    /// <para><b>Terminal.</b> Recovery is the <see cref="Device.IsLost"/>
    /// policy: dispose every dependent resource, dispose the
    /// <see cref="Device"/>, and rebuild from a fresh
    /// <see cref="PhysicalDevice"/>. <see cref="Swapchain.Dispose"/> is legal
    /// here and destroys whatever is still held — which depends on where this
    /// state was entered, per the note on the enum.</para>
    /// <para><see cref="Device.IsLost"/> is deliberately over-broad in a
    /// multi-device process (a loss observed at the context-free throw site
    /// marks every live device), so it is not a substitute for reading this
    /// state: it answers "has any device died?", this member answers "did
    /// <i>this</i> swapchain die of it?".</para>
    /// </summary>
    DeviceLost,
}
