namespace Ahjo.Vulkan.Vma.Native;

public unsafe partial struct VmaDefragmentationMove
{
    public VmaDefragmentationMoveOperation operation;

    [NativeTypeName("VmaAllocation _Nonnull")]
    public VmaAllocation_T* srcAllocation;

    [NativeTypeName("VmaAllocation _Nonnull")]
    public VmaAllocation_T* dstTmpAllocation;
}
