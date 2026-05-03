namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorGetTensorInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkTensorViewARM")]
    public VkTensorViewARM_T* tensorView;
}
