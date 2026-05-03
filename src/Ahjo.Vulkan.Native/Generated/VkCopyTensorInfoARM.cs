namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyTensorInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkTensorARM")]
    public VkTensorARM_T* srcTensor;

    [NativeTypeName("VkTensorARM")]
    public VkTensorARM_T* dstTensor;

    [NativeTypeName("uint32_t")]
    public uint regionCount;

    [NativeTypeName("const VkTensorCopyARM *")]
    public VkTensorCopyARM* pRegions;
}
