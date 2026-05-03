namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkWriteDescriptorSetTensorARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint tensorViewCount;

    [NativeTypeName("const VkTensorViewARM *")]
    public VkTensorViewARM_T** pTensorViews;
}
