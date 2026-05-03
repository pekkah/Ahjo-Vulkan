namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorBufferBindingInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceAddress")]
    public ulong address;

    [NativeTypeName("VkBufferUsageFlags")]
    public uint usage;
}
