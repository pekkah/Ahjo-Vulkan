using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One region for <see cref="CommandRecorder.CopyBufferToImage"/> /
/// <see cref="CommandRecorder.CopyImageToBuffer"/>. Maps onto
/// <c>VkBufferImageCopy2</c>. <see cref="RowLength"/> /
/// <see cref="ImageHeight"/> default to zero (tightly packed); set
/// non-zero only for partial uploads from a larger buffer layout.
/// </summary>
public readonly record struct BufferImageCopy
{
    public ulong                  BufferOffset   { get; init; }
    /// <summary>Tightly packed when zero — the typical case.</summary>
    public uint                   RowLength      { get; init; }
    /// <summary>Tightly packed when zero — the typical case.</summary>
    public uint                   ImageHeight    { get; init; }
    public VkImageAspectFlagBits  Aspect         { get; init; }
    public uint                   MipLevel       { get; init; }
    public uint                   BaseArrayLayer { get; init; }
    public uint                   LayerCount     { get; init; }
    public VkOffset3D             ImageOffset    { get; init; }
    public VkExtent3D             ImageExtent    { get; init; }

    /// <summary>
    /// Whole-image copy of mip 0, layer 0, color aspect, with no buffer
    /// row-padding. Pass <paramref name="aspect"/> for depth or stencil.
    /// </summary>
    public static BufferImageCopy WholeImage(
        in Image                image,
        ulong                   bufferOffset = 0,
        VkImageAspectFlagBits   aspect       = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT)
        => new()
        {
            BufferOffset   = bufferOffset,
            Aspect         = aspect,
            MipLevel       = 0,
            BaseArrayLayer = 0,
            LayerCount     = image.ArrayLayers == 0 ? 1u : image.ArrayLayers,
            ImageExtent    = new VkExtent3D
            {
                width  = image.Width,
                height = image.Height,
                depth  = image.Depth == 0 ? 1u : image.Depth,
            },
        };

    internal VkBufferImageCopy2 ToNative() => new()
    {
        sType             = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_IMAGE_COPY_2,
        bufferOffset      = BufferOffset,
        bufferRowLength   = RowLength,
        bufferImageHeight = ImageHeight,
        imageSubresource  = new VkImageSubresourceLayers
        {
            aspectMask     = (uint)Aspect,
            mipLevel       = MipLevel,
            baseArrayLayer = BaseArrayLayer,
            layerCount     = LayerCount == 0 ? 1u : LayerCount,
        },
        imageOffset = ImageOffset,
        imageExtent = ImageExtent,
    };
}
