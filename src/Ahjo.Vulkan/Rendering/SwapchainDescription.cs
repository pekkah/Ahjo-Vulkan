using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="Swapchain.Swapchain(Device, in SwapchainDescription)"/>.
/// All fields are advisory — the constructor negotiates with the
/// surface caps and falls back to a known-good default when the
/// requested value isn't supported.
/// </summary>
public ref struct SwapchainDescription
{
    /// <summary>Required. The platform surface this swapchain renders to.</summary>
    public Surface Surface;

    /// <summary>
    /// Preferred image count. <c>0</c> means "<c>caps.minImageCount + 1</c>"
    /// — the typical "double-buffer minimum, give me triple if possible"
    /// recipe. Clamped to <c>caps.minImageCount..caps.maxImageCount</c>.
    /// </summary>
    public uint PreferredImageCount;

    /// <summary>
    /// Preferred surface formats in priority order. The constructor walks
    /// this list and picks the first one the surface reports as supported.
    /// Default (empty span) means "first format from
    /// <c>vkGetPhysicalDeviceSurfaceFormatsKHR</c>" — drivers list their
    /// preferred format first.
    ///
    /// Pass multiple sRGB variants to stay portable across drivers /
    /// platforms — e.g. <c>[B8G8R8A8_SRGB, R8G8B8A8_SRGB]</c> covers
    /// desktop NVIDIA / AMD / Intel and the BGRA-light mobile / tile-
    /// based GPUs.
    /// </summary>
    public ReadOnlySpan<VkSurfaceFormatKHR> PreferredFormats;

    /// <summary>
    /// Preferred present mode. Default
    /// (<c>VK_PRESENT_MODE_FIFO_KHR</c>) is guaranteed available per the
    /// spec — anyone wanting <c>MAILBOX</c> or <c>IMMEDIATE</c> sets it
    /// explicitly and accepts the FIFO fallback.
    /// </summary>
    public VkPresentModeKHR PreferredPresentMode;

    /// <summary>
    /// Image usage flags. Default <see cref="ImageUsage.ColorAttachment"/>
    /// — sufficient for the dynamic-rendering color-out path. Add
    /// <see cref="ImageUsage.TransferSrc"/> for screenshot/blit-out
    /// flows.
    /// </summary>
    public ImageUsage ImageUsage;

    /// <summary>
    /// Requested width / height in pixels. The constructor uses
    /// <c>caps.currentExtent</c> when the surface reports a fixed
    /// extent (the common case on Windows / desktop Linux), and falls
    /// back to <c>(Width, Height)</c> clamped to caps when the surface
    /// reports the "application chooses" sentinel
    /// (<c>0xFFFFFFFF, 0xFFFFFFFF</c>) — typical of mobile/Wayland.
    /// </summary>
    public uint Width;
    public uint Height;
}
