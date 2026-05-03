namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSemaphoreSubmitInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSemaphore")]
    public VkSemaphore_T* semaphore;

    [NativeTypeName("uint64_t")]
    public ulong value;

    [NativeTypeName("VkPipelineStageFlags2")]
    public ulong stageMask;

    [NativeTypeName("uint32_t")]
    public uint deviceIndex;
}
