namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSemaphoreSignalInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSemaphore")]
    public VkSemaphore_T* semaphore;

    [NativeTypeName("uint64_t")]
    public ulong value;
}
