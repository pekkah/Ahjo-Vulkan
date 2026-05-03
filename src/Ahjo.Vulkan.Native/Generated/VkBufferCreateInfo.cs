namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBufferCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBufferCreateFlags")]
    public uint flags;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;

    [NativeTypeName("VkBufferUsageFlags")]
    public uint usage;

    public VkSharingMode sharingMode;

    [NativeTypeName("uint32_t")]
    public uint queueFamilyIndexCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pQueueFamilyIndices;
}
