namespace Ahjo.Vulkan.Vma.Native;

public unsafe partial struct VmaDefragmentationPassMoveInfo
{
    [NativeTypeName("uint32_t")]
    public uint moveCount;

    [NativeTypeName("VmaDefragmentationMove * _Nullable")]
    public VmaDefragmentationMove* pMoves;
}
