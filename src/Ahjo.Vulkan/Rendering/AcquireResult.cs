namespace Ahjo.Vulkan;

/// <summary>
/// Outcome of <see cref="Swapchain.AcquireNextImage"/>. Mirrors the
/// distinct <see cref="Native.VkResult"/> values that
/// <c>vkAcquireNextImageKHR</c> reports as informational rather than
/// hard failures, plus a synthetic <see cref="Timeout"/> for the
/// CPU-side wait expiring.
/// </summary>
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
    /// <c>VkResult</c>.</summary>
    SurfaceLost,
    /// <summary>CPU-side timeout elapsed without an image becoming
    /// available.</summary>
    Timeout,
    /// <summary>Non-blocking call (<c>timeout = 0</c>) found no image
    /// ready yet.</summary>
    NotReady,
}
