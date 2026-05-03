namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageViewAddressPropertiesNVX
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceAddress")]
    public ulong deviceAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;
}
