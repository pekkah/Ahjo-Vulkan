using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One buffer-memory barrier for sync2. Maps onto
/// <c>VkBufferMemoryBarrier2</c>. <see cref="Size"/> defaults to
/// <c>VK_WHOLE_SIZE</c> (~0UL) when zero — covers the dominant case.
/// </summary>
/// <remarks>
/// <see cref="For"/> covers single-queue ordering; <see cref="Release"/>
/// and <see cref="Acquire"/> form the pair that hands a buffer between
/// queue families when <c>VK_SHARING_MODE_EXCLUSIVE</c> is in effect.
/// Direct construction via <c>new BufferBarrier { … }</c> requires
/// setting <see cref="SrcQueueFamilyIndex"/> and
/// <see cref="DstQueueFamilyIndex"/> explicitly; queue family <c>0</c>
/// is a valid index, so the wrapper does not substitute
/// <c>VK_QUEUE_FAMILY_IGNORED</c> for the zero default.
/// </remarks>
public unsafe readonly record struct BufferBarrier
{
    /// <summary>Sentinel matching <c>VK_QUEUE_FAMILY_IGNORED</c>.</summary>
    public const uint QueueFamilyIgnored = ~0u;

    /// <summary>Raw <c>VkBuffer_T*</c> stored as <c>nint</c> (records reject pointer fields).</summary>
    public nint        Buffer              { get; init; }
    public Stage       SrcStage            { get; init; }
    public Access      SrcAccess           { get; init; }
    public Stage       DstStage            { get; init; }
    public Access      DstAccess           { get; init; }
    public uint        SrcQueueFamilyIndex { get; init; }
    public uint        DstQueueFamilyIndex { get; init; }
    public ulong       Offset              { get; init; }
    public ulong       Size                { get; init; }

    /// <summary>
    /// Same-queue producer/consumer ordering. Both queue family indices
    /// land as <c>VK_QUEUE_FAMILY_IGNORED</c>; for cross-family handoff
    /// use <see cref="Release"/> / <see cref="Acquire"/>.
    /// </summary>
    public static BufferBarrier For(
        in Buffer buffer,
        Stage     srcStage, Access srcAccess,
        Stage     dstStage, Access dstAccess)
        => new()
        {
            Buffer              = (nint)buffer.Handle,
            SrcStage            = srcStage, SrcAccess = srcAccess,
            DstStage            = dstStage, DstAccess = dstAccess,
            SrcQueueFamilyIndex = QueueFamilyIgnored,
            DstQueueFamilyIndex = QueueFamilyIgnored,
            Offset              = 0,
            Size                = ~0ul, // VK_WHOLE_SIZE
        };

    /// <summary>
    /// Release half of a queue-family ownership transfer — recorded on
    /// the source queue. Vulkan §7.7.4: a release has no destination
    /// stage/access (the consumer specifies those on its acquire), so
    /// the factory zeros <see cref="DstStage"/> / <see cref="DstAccess"/>.
    /// </summary>
    public static BufferBarrier Release(
        in Buffer buffer,
        uint      fromQueueFamily,
        uint      toQueueFamily,
        Stage     srcStage,
        Access    srcAccess,
        ulong     offset = 0,
        ulong     size   = ~0ul)
        => new()
        {
            Buffer              = (nint)buffer.Handle,
            SrcStage            = srcStage,
            SrcAccess           = srcAccess,
            DstStage            = Stage.None,
            DstAccess           = Access.None,
            SrcQueueFamilyIndex = fromQueueFamily,
            DstQueueFamilyIndex = toQueueFamily,
            Offset              = offset,
            Size                = size,
        };

    /// <summary>
    /// Acquire half of a queue-family ownership transfer — recorded on
    /// the destination queue. Vulkan §7.7.4: an acquire has no source
    /// stage/access (the producer specified those on its release), so
    /// the factory zeros <see cref="SrcStage"/> / <see cref="SrcAccess"/>.
    /// </summary>
    public static BufferBarrier Acquire(
        in Buffer buffer,
        uint      fromQueueFamily,
        uint      toQueueFamily,
        Stage     dstStage,
        Access    dstAccess,
        ulong     offset = 0,
        ulong     size   = ~0ul)
        => new()
        {
            Buffer              = (nint)buffer.Handle,
            SrcStage            = Stage.None,
            SrcAccess           = Access.None,
            DstStage            = dstStage,
            DstAccess           = dstAccess,
            SrcQueueFamilyIndex = fromQueueFamily,
            DstQueueFamilyIndex = toQueueFamily,
            Offset              = offset,
            Size                = size,
        };

    internal VkBufferMemoryBarrier2 ToNative() => new()
    {
        sType               = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_MEMORY_BARRIER_2,
        srcStageMask        = (ulong)SrcStage,
        srcAccessMask       = (ulong)SrcAccess,
        dstStageMask        = (ulong)DstStage,
        dstAccessMask       = (ulong)DstAccess,
        srcQueueFamilyIndex = SrcQueueFamilyIndex,
        dstQueueFamilyIndex = DstQueueFamilyIndex,
        buffer              = (VkBuffer_T*)Buffer,
        offset              = Offset,
        size                = Size == 0 ? ~0ul : Size,
    };
}
