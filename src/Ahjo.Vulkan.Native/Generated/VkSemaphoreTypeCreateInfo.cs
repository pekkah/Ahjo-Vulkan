namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSemaphoreTypeCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkSemaphoreType semaphoreType;

    [NativeTypeName("uint64_t")]
    public ulong initialValue;
}
