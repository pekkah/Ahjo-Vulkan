namespace Ahjo.Vulkan;

/// <summary>
/// Lifecycle state of a <see cref="Swapchain"/> (issue #120). Makes the
/// "window minimized" and "recreate failed" situations legal states with
/// documented transitions instead of corrupted object state.
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
    /// A <see cref="Swapchain.Recreate"/> failed after the old swapchain
    /// was retired (issue #112), the surface was lost, or the device was
    /// lost. No swapchain handle is held; acquire/present throw
    /// <see cref="InvalidOperationException"/>.
    /// <see cref="Swapchain.Recreate"/> attempts a from-scratch create
    /// (surface/device loss permitting); <see cref="Swapchain.Dispose"/>
    /// is always legal.
    /// </summary>
    Poisoned,
}
