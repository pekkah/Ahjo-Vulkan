using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="Image.CreateView"/>. Maps onto
/// <c>VkImageViewCreateInfo</c> minus the boilerplate (<c>sType</c>,
/// <c>pNext</c>, <c>flags</c>, the source <c>VkImage</c> handle, and
/// component swizzle which defaults to identity).
/// </summary>
/// <remarks>
/// <para><see cref="Format"/> defaults to <c>VK_FORMAT_UNDEFINED</c> (zero),
/// which <see cref="Image.CreateView"/> interprets as "use the parent image's
/// format" — the dominant case. Set explicitly only when the view is meant
/// to reinterpret the storage (mutable-format images).</para>
/// <para><b>Valid-by-default (issue #119):</b> <c>new ImageViewDescription { … }</c>
/// covers the whole image as a 2D view, so the typical call sets only
/// <see cref="Aspect"/>. The subresource-range and view-type fields default to
/// values that map to a valid <c>VkImageViewCreateInfo</c>; a zero-default here
/// used to produce <c>levelCount = 0</c> (invalid).</para>
/// </remarks>
public readonly record struct ImageViewDescription
{
    /// <summary>
    /// View dimensionality. Defaults to <c>VK_IMAGE_VIEW_TYPE_2D</c> — the
    /// dominant case. Set explicitly for 1D/3D/cube/array views.
    /// </summary>
    public VkImageViewType   ViewType       { get; init; } = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D;

    /// <summary>Format override. <c>VK_FORMAT_UNDEFINED</c> = inherit from image.</summary>
    public VkFormat          Format         { get; init; }

    /// <summary>Aspect bits (color, depth, stencil).</summary>
    public VkImageAspectFlagBits Aspect     { get; init; }

    public uint BaseMipLevel   { get; init; }

    /// <summary>
    /// Number of mip levels. Defaults to <c>VK_REMAINING_MIP_LEVELS</c> — the
    /// view covers every level from <see cref="BaseMipLevel"/> onward.
    /// </summary>
    public uint LevelCount     { get; init; } = Vk.VK_REMAINING_MIP_LEVELS;

    public uint BaseArrayLayer { get; init; }

    /// <summary>
    /// Number of array layers. Defaults to <c>VK_REMAINING_ARRAY_LAYERS</c> —
    /// the view covers every layer from <see cref="BaseArrayLayer"/> onward.
    /// </summary>
    public uint LayerCount     { get; init; } = Vk.VK_REMAINING_ARRAY_LAYERS;

    /// <summary>
    /// Runs the valid-by-default field initializers (issue #119) — required
    /// explicitly for a struct with field initializers (CS8983). Gives
    /// <c>new ImageViewDescription { … }</c> a whole-image 2D view by default.
    /// </summary>
    public ImageViewDescription() { }
}
