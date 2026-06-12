using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One image-memory barrier for sync2 pipeline barriers. Maps onto
/// <c>VkImageMemoryBarrier2</c> minus the boilerplate (<c>sType</c>,
/// <c>pNext</c>).
/// </summary>
/// <remarks>
/// <para>The <see cref="Transition"/> factory pre-fills the most common
/// mistake-magnet fields (full subresource range, color aspect,
/// queue-ownership ignored on both sides). For multi-queue handoff use
/// <see cref="Release"/> on the source queue and <see cref="Acquire"/> on
/// the destination queue — those factories also encode the spec's
/// stage/access asymmetry (release zeros <c>dst</c>, acquire zeros
/// <c>src</c>) so callers can't accidentally over-specify.</para>
/// <para>Direct construction via <c>new ImageBarrier { … }</c> requires
/// setting <see cref="SrcQueueFamilyIndex"/> and
/// <see cref="DstQueueFamilyIndex"/> explicitly; the wrapper does not
/// substitute <c>VK_QUEUE_FAMILY_IGNORED</c> for the zero-default because
/// queue family <c>0</c> is a valid index.</para>
/// </remarks>
public unsafe readonly record struct ImageBarrier
{
    /// <summary>Sentinel matching <c>VK_QUEUE_FAMILY_IGNORED</c>.</summary>
    public const uint QueueFamilyIgnored = ~0u;

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
    public uint                   SrcQueueFamilyIndex { get; init; }
    public uint                   DstQueueFamilyIndex { get; init; }
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
    /// explicit, separate flow via <see cref="Release"/> / <see cref="Acquire"/>.
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
            Image               = (nint)image.Handle,
            SrcStage            = srcStage,
            SrcAccess           = srcAccess,
            DstStage            = dstStage,
            DstAccess           = dstAccess,
            OldLayout           = from,
            NewLayout           = to,
            SrcQueueFamilyIndex = QueueFamilyIgnored,
            DstQueueFamilyIndex = QueueFamilyIgnored,
            Aspect              = aspect,
            BaseMipLevel        = 0,
            LevelCount          = image.MipLevels,
            BaseArrayLayer      = 0,
            LayerCount          = image.ArrayLayers,
        };

    /// <summary>
    /// Release half of a queue-family ownership transfer — recorded on
    /// the source queue. Per Vulkan §7.7.4 a release has no destination
    /// stage/access (the consumer specifies those on its acquire), so the
    /// factory zeros <see cref="DstStage"/> / <see cref="DstAccess"/>
    /// implicitly. The matching <see cref="Acquire"/> on the destination
    /// queue must use the same <paramref name="from"/>/<paramref name="to"/>
    /// layouts and the same queue-family pair.
    /// </summary>
    public static ImageBarrier Release(
        in Image                image,
        VkImageLayout           from,
        VkImageLayout           to,
        uint                    fromQueueFamily,
        uint                    toQueueFamily,
        Stage                   srcStage,
        Access                  srcAccess,
        VkImageAspectFlagBits   aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT)
        => new()
        {
            Image               = (nint)image.Handle,
            SrcStage            = srcStage,
            SrcAccess           = srcAccess,
            DstStage            = Stage.None,
            DstAccess           = Access.None,
            OldLayout           = from,
            NewLayout           = to,
            SrcQueueFamilyIndex = fromQueueFamily,
            DstQueueFamilyIndex = toQueueFamily,
            Aspect              = aspect,
            BaseMipLevel        = 0,
            LevelCount          = image.MipLevels,
            BaseArrayLayer      = 0,
            LayerCount          = image.ArrayLayers,
        };

    /// <summary>
    /// Acquire half of a queue-family ownership transfer — recorded on
    /// the destination queue. Per Vulkan §7.7.4 an acquire has no source
    /// stage/access (the producer specified those on its release), so the
    /// factory zeros <see cref="SrcStage"/> / <see cref="SrcAccess"/>
    /// implicitly.
    /// </summary>
    public static ImageBarrier Acquire(
        in Image                image,
        VkImageLayout           from,
        VkImageLayout           to,
        uint                    fromQueueFamily,
        uint                    toQueueFamily,
        Stage                   dstStage,
        Access                  dstAccess,
        VkImageAspectFlagBits   aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT)
        => new()
        {
            Image               = (nint)image.Handle,
            SrcStage            = Stage.None,
            SrcAccess           = Access.None,
            DstStage            = dstStage,
            DstAccess           = dstAccess,
            OldLayout           = from,
            NewLayout           = to,
            SrcQueueFamilyIndex = fromQueueFamily,
            DstQueueFamilyIndex = toQueueFamily,
            Aspect              = aspect,
            BaseMipLevel        = 0,
            LevelCount          = image.MipLevels,
            BaseArrayLayer      = 0,
            LayerCount          = image.ArrayLayers,
        };

    internal VkImageMemoryBarrier2 ToNative()
    {
        // Aspect=0 used to silently default to COLOR, which silently
        // miscompiled depth/stencil object-initializer barriers
        // (VUID-VkImageSubresourceRange-aspectMask-requiredbitmask).
        // Throwing surfaces the missing field instead of producing a
        // legal-but-wrong COLOR barrier on a depth image.
        if (Aspect == 0)
            throw new InvalidOperationException(
                "ImageBarrier.Aspect must be set explicitly (e.g. VK_IMAGE_ASPECT_COLOR_BIT). " +
                "Use Transition/Release/Acquire factories or set Aspect in the object initializer.");

        return new VkImageMemoryBarrier2
        {
            sType               = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER_2,
            srcStageMask        = (ulong)SrcStage,
            srcAccessMask       = (ulong)SrcAccess,
            dstStageMask        = (ulong)DstStage,
            dstAccessMask       = (ulong)DstAccess,
            oldLayout           = OldLayout,
            newLayout           = NewLayout,
            srcQueueFamilyIndex = SrcQueueFamilyIndex,
            dstQueueFamilyIndex = DstQueueFamilyIndex,
            image               = (VkImage_T*)Image,
            // levelCount/layerCount fall back to 1 when zero: belt-and-braces
            // for a default(ImageBarrier) element in a span, which bypasses the
            // valid-by-default convention's field initializers (issue #119).
            // Factories and direct `new ImageBarrier { … }` set real counts.
            subresourceRange    = new VkImageSubresourceRange
            {
                aspectMask     = (uint)Aspect,
                baseMipLevel   = BaseMipLevel,
                levelCount     = LevelCount == 0 ? 1u : LevelCount,
                baseArrayLayer = BaseArrayLayer,
                layerCount     = LayerCount == 0 ? 1u : LayerCount,
            },
        };
    }
}
