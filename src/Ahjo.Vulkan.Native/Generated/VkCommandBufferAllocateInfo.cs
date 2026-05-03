namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCommandBufferAllocateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkCommandPool")]
    public VkCommandPool_T* commandPool;

    public VkCommandBufferLevel level;

    [NativeTypeName("uint32_t")]
    public uint commandBufferCount;
}
