using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One image-memory barrier for sync2 pipeline barriers. Maps onto
/// <c>VkImageMemoryBarrier2</c> minus the boilerplate (<c>sType</c>,
/// <c>pNext</c>) and queue-ownership transfer (treated as
/// <c>VK_QUEUE_FAMILY_IGNORED</c> here; ownership transfers are filed as
/// a follow-up alongside multi-queue submission).
/// </summary>
/// <remarks>
/// The <see cref="Transition(in Image, VkImageLayout, VkImageLayout, Stage, Access, Stage, Access, VkImageAspectFlagBits)"/>
/// factory pre-fills the most common mistake-magnet fields (full
/// subresource range, color aspect, queue-ownership ignored). For
/// transfers across queues, set <see cref="Image"/>, ranges, and stage/
/// access masks explicitly.
/// </remarks>
public unsafe readonly record struct ImageBarrier
{
    /// <summary>
    /// Raw <c>VkImage_T*</c> stored as <c>nint</c> because records reject
    /// pointer-typed fields. Cast at the boundary in <see cref="ToNative"/>.
    /// </summary>
    public nint                   Image          { get; init; }
    public Stage                  SrcStage       { get; init; }
    public Access                 SrcAccess      { get; init; }
    public Stage                  DstStage       { get; init; }
    public Access                 DstAccess      { get; init; }
    public VkImageLayout          OldLayout      { get; init; }
    public VkImageLayout          NewLayout      { get; init; }
    public VkImageAspectFlagBits  Aspect         { get; init; }
    public uint                   BaseMipLevel   { get; init; }
    public uint                   LevelCount     { get; init; }
    public uint                   BaseArrayLayer { get; init; }
    public uint                   LayerCount     { get; init; }

    /// <summary>
    /// Standard layout transition with full subresource range (color
    /// aspect by default; pass <paramref name="aspect"/> for depth or
    /// stencil targets). Both queue family indices land as
    /// <c>VK_QUEUE_FAMILY_IGNORED</c> — ownership transfers are an
    /// explicit, separate flow.
    /// </summary>
    public static ImageBarrier Transition(
        in Image                image,
        VkImageLayout           from,
        VkImageLayout           to,
        Stage                   srcStage,
        Access                  srcAccess,
        Stage                   dstStage,
        Access                  dstAccess,
        VkImageAspectFlagBits   aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT)
        => new()
        {
            Image          = (nint)image.Handle,
            SrcStage       = srcStage,
            SrcAccess      = srcAccess,
            DstStage       = dstStage,
            DstAccess      = dstAccess,
            OldLayout      = from,
            NewLayout      = to,
            Aspect         = aspect,
            BaseMipLevel   = 0,
            LevelCount     = image.MipLevels == 0 ? 1u : image.MipLevels,
            BaseArrayLayer = 0,
            LayerCount     = image.ArrayLayers == 0 ? 1u : image.ArrayLayers,
        };

    internal VkImageMemoryBarrier2 ToNative() => new()
    {
        sType            = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER_2,
        srcStageMask     = (ulong)SrcStage,
        srcAccessMask    = (ulong)SrcAccess,
        dstStageMask     = (ulong)DstStage,
        dstAccessMask    = (ulong)DstAccess,
        oldLayout        = OldLayout,
        newLayout        = NewLayout,
        srcQueueFamilyIndex = ~0u, // VK_QUEUE_FAMILY_IGNORED
        dstQueueFamilyIndex = ~0u,
        image            = (VkImage_T*)Image,
        subresourceRange = new VkImageSubresourceRange
        {
            aspectMask     = (uint)Aspect,
            baseMipLevel   = BaseMipLevel,
            levelCount     = LevelCount == 0 ? 1u : LevelCount,
            baseArrayLayer = BaseArrayLayer,
            layerCount     = LayerCount == 0 ? 1u : LayerCount,
        },
    };
}
