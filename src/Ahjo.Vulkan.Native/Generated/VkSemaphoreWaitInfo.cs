namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSemaphoreWaitInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSemaphoreWaitFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint semaphoreCount;

    [NativeTypeName("const VkSemaphore *")]
    public VkSemaphore_T** pSemaphores;

    [NativeTypeName("const uint64_t *")]
    public ulong* pValues;
}
