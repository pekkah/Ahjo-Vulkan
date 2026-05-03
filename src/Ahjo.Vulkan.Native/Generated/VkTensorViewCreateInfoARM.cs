namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkTensorViewCreateInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkTensorViewCreateFlagsARM")]
    public ulong flags;

    [NativeTypeName("VkTensorARM")]
    public VkTensorARM_T* tensor;

    public VkFormat format;
}
