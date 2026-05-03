namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBufferMemoryBarrier2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineStageFlags2")]
    public ulong srcStageMask;

    [NativeTypeName("VkAccessFlags2")]
    public ulong srcAccessMask;

    [NativeTypeName("VkPipelineStageFlags2")]
    public ulong dstStageMask;

    [NativeTypeName("VkAccessFlags2")]
    public ulong dstAccessMask;

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
