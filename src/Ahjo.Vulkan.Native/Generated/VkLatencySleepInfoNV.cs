namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkLatencySleepInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSemaphore")]
    public VkSemaphore_T* signalSemaphore;

    [NativeTypeName("uint64_t")]
    public ulong value;
}
