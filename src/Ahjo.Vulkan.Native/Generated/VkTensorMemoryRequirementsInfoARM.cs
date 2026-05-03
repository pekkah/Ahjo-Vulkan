namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkTensorMemoryRequirementsInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkTensorARM")]
    public VkTensorARM_T* tensor;
}
