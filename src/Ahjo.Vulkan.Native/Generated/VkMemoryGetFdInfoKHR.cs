namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMemoryGetFdInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceMemory")]
    public VkDeviceMemory_T* memory;

    public VkExternalMemoryHandleTypeFlagBits handleType;
}
