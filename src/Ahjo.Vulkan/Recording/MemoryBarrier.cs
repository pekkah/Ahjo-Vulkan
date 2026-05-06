using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One global memory barrier for sync2. Maps onto
/// <c>VkMemoryBarrier2</c>. Useful when ordering is needed between
/// stages but no specific buffer or image needs to participate
/// (e.g. fence-like ordering across an unrelated set of resources).
/// </summary>
public readonly record struct MemoryBarrier
{
    public Stage  SrcStage  { get; init; }
    public Access SrcAccess { get; init; }
    public Stage  DstStage  { get; init; }
    public Access DstAccess { get; init; }

    public static MemoryBarrier Between(
        Stage  srcStage, Access srcAccess,
        Stage  dstStage, Access dstAccess)
        => new() { SrcStage = srcStage, SrcAccess = srcAccess, DstStage = dstStage, DstAccess = dstAccess };

    internal VkMemoryBarrier2 ToNative() => new()
    {
        sType         = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_BARRIER_2,
        srcStageMask  = (ulong)SrcStage,
        srcAccessMask = (ulong)SrcAccess,
        dstStageMask  = (ulong)DstStage,
        dstAccessMask = (ulong)DstAccess,
    };
}
