namespace Ahjo.Vulkan;

/// <summary>
/// Caller-supplied "drain GPU work that references the old swapchain" hook
/// for <see cref="Swapchain.Recreate"/>. Replaces the default
/// <c>vkDeviceWaitIdle</c> sledgehammer with whatever fine-grained wait
/// the caller's frame loop already maintains — typically the per-slot
/// in-flight fences on a <see cref="FrameRing"/> (see
/// <see cref="FrameRing.WaitForInFlightFences"/>).
/// </summary>
/// <remarks>
/// The callback must block until every queue submission that references
/// the swapchain image being destroyed has finished executing. A wait
/// that misses pending submits leaves the driver tearing down a live
/// dependency and will surface as <c>VK_ERROR_DEVICE_LOST</c> or a
/// validation-layer error on the next frame. Conversely, waiting on
/// more than the swapchain submits is harmless — the default
/// <c>vkDeviceWaitIdle</c> path is the maximally conservative choice.
/// </remarks>
public delegate void SwapchainSyncCallback();
