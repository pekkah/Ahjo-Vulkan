namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBindTensorMemoryInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkTensorARM")]
    public VkTensorARM_T* tensor;

    [NativeTypeName("VkDeviceMemory")]
    public VkDeviceMemory_T* memory;

    [NativeTypeName("VkDeviceSize")]
    public ulong memoryOffset;
}
