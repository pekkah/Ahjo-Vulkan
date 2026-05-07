using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One region for <see cref="CommandRecorder.BlitImage"/>. Maps onto
/// <c>VkImageBlit2</c>. Each side carries two corner offsets defining
/// an axis-aligned box; the source box is sampled (with the filter the
/// recorder picks) into the destination box.
/// </summary>
public readonly record struct ImageBlitRegion
{
    public VkImageAspectFlagBits  SrcAspect         { get; init; }
    public uint                   SrcMipLevel       { get; init; }
    public uint                   SrcBaseArrayLayer { get; init; }
    public uint                   SrcLayerCount     { get; init; }
    public VkOffset3D             SrcOffset0        { get; init; }
    public VkOffset3D             SrcOffset1        { get; init; }

    public VkImageAspectFlagBits  DstAspect         { get; init; }
    public uint                   DstMipLevel       { get; init; }
    public uint                   DstBaseArrayLayer { get; init; }
    public uint                   DstLayerCount     { get; init; }
    public VkOffset3D             DstOffset0        { get; init; }
    public VkOffset3D             DstOffset1        { get; init; }

    /// <summary>
    /// Full-image color blit (mip 0, layer 0) — useful for swapchain-style
    /// rescaling. Caller must ensure <paramref name="src"/> and
    /// <paramref name="dst"/> have compatible formats and that
    /// <see cref="ImageUsage.TransferSrc"/> / <see cref="ImageUsage.TransferDst"/>
    /// were declared.
    /// </summary>
    public static ImageBlitRegion WholeImage(
        in Image src,
        in Image dst,
        VkImageAspectFlagBits aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT)
        => new()
        {
            SrcAspect     = aspect,
            SrcLayerCount = src.ArrayLayers == 0 ? 1u : src.ArrayLayers,
            SrcOffset1    = new VkOffset3D
            {
                x = (int)src.Width,
                y = (int)src.Height,
                z = (int)(src.Depth == 0 ? 1u : src.Depth),
            },
            DstAspect     = aspect,
            DstLayerCount = dst.ArrayLayers == 0 ? 1u : dst.ArrayLayers,
            DstOffset1    = new VkOffset3D
            {
                x = (int)dst.Width,
                y = (int)dst.Height,
                z = (int)(dst.Depth == 0 ? 1u : dst.Depth),
            },
        };

    internal VkImageBlit2 ToNative()
    {
        var b = new VkImageBlit2
        {
            sType          = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_BLIT_2,
            srcSubresource = new VkImageSubresourceLayers
            {
                // See ImageBarrier.ToNative — record-zero-init aspect=0
                // lands as a VUID reject; default to COLOR to match
                // WholeImage and give object-initializer callers the
                // dominant case.
                aspectMask     = SrcAspect == 0 ? (uint)VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT : (uint)SrcAspect,
                mipLevel       = SrcMipLevel,
                baseArrayLayer = SrcBaseArrayLayer,
                layerCount     = SrcLayerCount == 0 ? 1u : SrcLayerCount,
            },
            dstSubresource = new VkImageSubresourceLayers
            {
                aspectMask     = DstAspect == 0 ? (uint)VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT : (uint)DstAspect,
                mipLevel       = DstMipLevel,
                baseArrayLayer = DstBaseArrayLayer,
                layerCount     = DstLayerCount == 0 ? 1u : DstLayerCount,
            },
        };
        b.srcOffsets[0] = SrcOffset0;
        b.srcOffsets[1] = SrcOffset1;
        b.dstOffsets[0] = DstOffset0;
        b.dstOffsets[1] = DstOffset1;
        return b;
    }
}
