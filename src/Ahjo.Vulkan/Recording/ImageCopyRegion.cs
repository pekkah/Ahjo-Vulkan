using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One region for <see cref="CommandRecorder.CopyImage"/>. Maps onto
/// <c>VkImageCopy2</c>. Both subresource sides default to mip 0,
/// layer 0, color aspect with single layer — set explicitly for
/// mip-to-mip or array-slice copies.
/// </summary>
public readonly record struct ImageCopyRegion
{
    public VkImageAspectFlagBits  SrcAspect         { get; init; }
    public uint                   SrcMipLevel       { get; init; }
    public uint                   SrcBaseArrayLayer { get; init; }
    public uint                   SrcLayerCount     { get; init; }
    public VkOffset3D             SrcOffset         { get; init; }

    public VkImageAspectFlagBits  DstAspect         { get; init; }
    public uint                   DstMipLevel       { get; init; }
    public uint                   DstBaseArrayLayer { get; init; }
    public uint                   DstLayerCount     { get; init; }
    public VkOffset3D             DstOffset         { get; init; }

    public VkExtent3D             Extent            { get; init; }

    /// <summary>
    /// Whole-image color copy of mip 0, layer 0 from <paramref name="src"/>'s
    /// extent into <paramref name="dst"/> at the origin. Convenience for
    /// the dominant single-region case.
    /// </summary>
    public static ImageCopyRegion WholeImage(
        in Image src,
        in Image dst,
        VkImageAspectFlagBits aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT)
        => new()
        {
            SrcAspect     = aspect,
            SrcLayerCount = src.ArrayLayers == 0 ? 1u : src.ArrayLayers,
            DstAspect     = aspect,
            DstLayerCount = dst.ArrayLayers == 0 ? 1u : dst.ArrayLayers,
            Extent        = new VkExtent3D
            {
                width  = src.Width,
                height = src.Height,
                depth  = src.Depth == 0 ? 1u : src.Depth,
            },
        };

    internal VkImageCopy2 ToNative() => new()
    {
        sType          = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_COPY_2,
        srcSubresource = new VkImageSubresourceLayers
        {
            aspectMask     = (uint)SrcAspect,
            mipLevel       = SrcMipLevel,
            baseArrayLayer = SrcBaseArrayLayer,
            layerCount     = SrcLayerCount == 0 ? 1u : SrcLayerCount,
        },
        srcOffset      = SrcOffset,
        dstSubresource = new VkImageSubresourceLayers
        {
            aspectMask     = (uint)DstAspect,
            mipLevel       = DstMipLevel,
            baseArrayLayer = DstBaseArrayLayer,
            layerCount     = DstLayerCount == 0 ? 1u : DstLayerCount,
        },
        dstOffset = DstOffset,
        extent    = Extent,
    };
}
