using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="Image.CreateView"/>. Maps onto
/// <c>VkImageViewCreateInfo</c> minus the boilerplate (<c>sType</c>,
/// <c>pNext</c>, <c>flags</c>, the source <c>VkImage</c> handle, and
/// component swizzle which defaults to identity).
/// </summary>
/// <remarks>
/// <see cref="Format"/> defaults to <c>VK_FORMAT_UNDEFINED</c> (zero), which
/// <see cref="Image.CreateView"/> interprets as "use the parent image's
/// format" — the dominant case. Set explicitly only when the view is meant
/// to reinterpret the storage (mutable-format images).
/// </remarks>
public readonly record struct ImageViewDescription
{
    /// <summary>
    /// View dimensionality. Defaults to <c>VK_IMAGE_VIEW_TYPE_1D</c> (zero) —
    /// callers should set this explicitly for any non-1D image.
    /// </summary>
    public VkImageViewType   ViewType       { get; init; }

    /// <summary>Format override. <c>VK_FORMAT_UNDEFINED</c> = inherit from image.</summary>
    public VkFormat          Format         { get; init; }

    /// <summary>Aspect bits (color, depth, stencil).</summary>
    public VkImageAspectFlagBits Aspect     { get; init; }

    public uint BaseMipLevel   { get; init; }
    public uint LevelCount     { get; init; }
    public uint BaseArrayLayer { get; init; }
    public uint LayerCount     { get; init; }
}
