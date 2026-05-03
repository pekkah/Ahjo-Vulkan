namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBufferMemoryBarrier
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkAccessFlags")]
    public uint srcAccessMask;

    [NativeTypeName("VkAccessFlags")]
    public uint dstAccessMask;

    [NativeTypeName("uint32_t")]
    public uint srcQueueFamilyIndex;

    [NativeTypeName("uint32_t")]
    public uint dstQueueFamilyIndex;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* buffer;

    [NativeTypeName("VkDeviceSize")]
    public ulong offset;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;
}
