namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMemoryGetRemoteAddressInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceMemory")]
    public VkDeviceMemory_T* memory;

    public VkExternalMemoryHandleTypeFlagBits handleType;
}
