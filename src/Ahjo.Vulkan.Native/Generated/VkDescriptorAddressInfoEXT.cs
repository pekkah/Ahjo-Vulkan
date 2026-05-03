namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorAddressInfoEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceAddress")]
    public ulong address;

    [NativeTypeName("VkDeviceSize")]
    public ulong range;

    public VkFormat format;
}
