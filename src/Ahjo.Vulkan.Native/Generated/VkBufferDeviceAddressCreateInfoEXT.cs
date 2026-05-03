namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBufferDeviceAddressCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceAddress")]
    public ulong deviceAddress;
}
