namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSemaphoreGetFdInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSemaphore")]
    public VkSemaphore_T* semaphore;

    public VkExternalSemaphoreHandleTypeFlagBits handleType;
}
