namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceExternalBufferInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBufferCreateFlags")]
    public uint flags;

    [NativeTypeName("VkBufferUsageFlags")]
    public uint usage;

    public VkExternalMemoryHandleTypeFlagBits handleType;
}
