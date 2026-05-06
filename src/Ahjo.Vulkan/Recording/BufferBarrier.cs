using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One buffer-memory barrier for sync2. Maps onto
/// <c>VkBufferMemoryBarrier2</c>. <see cref="Size"/> defaults to
/// <c>VK_WHOLE_SIZE</c> (~0UL) when zero — covers the dominant case.
/// </summary>
public unsafe readonly record struct BufferBarrier
{
    /// <summary>Raw <c>VkBuffer_T*</c> stored as <c>nint</c> (records reject pointer fields).</summary>
    public nint        Buffer    { get; init; }
    public Stage       SrcStage  { get; init; }
    public Access      SrcAccess { get; init; }
    public Stage       DstStage  { get; init; }
    public Access      DstAccess { get; init; }
    public ulong       Offset    { get; init; }
    public ulong       Size      { get; init; }

    public static BufferBarrier For(
        in Buffer buffer,
        Stage     srcStage, Access srcAccess,
        Stage     dstStage, Access dstAccess)
        => new()
        {
            Buffer    = (nint)buffer.Handle,
            SrcStage  = srcStage, SrcAccess = srcAccess,
            DstStage  = dstStage, DstAccess = dstAccess,
            Offset    = 0,
            Size      = ~0ul, // VK_WHOLE_SIZE
        };

    internal VkBufferMemoryBarrier2 ToNative() => new()
    {
        sType               = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_MEMORY_BARRIER_2,
        srcStageMask        = (ulong)SrcStage,
        srcAccessMask       = (ulong)SrcAccess,
        dstStageMask        = (ulong)DstStage,
        dstAccessMask       = (ulong)DstAccess,
        srcQueueFamilyIndex = ~0u,
        dstQueueFamilyIndex = ~0u,
        buffer              = (VkBuffer_T*)Buffer,
        offset              = Offset,
        size                = Size == 0 ? ~0ul : Size,
    };
}
