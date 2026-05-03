namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImportSemaphoreFdInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSemaphore")]
    public VkSemaphore_T* semaphore;

    [NativeTypeName("VkSemaphoreImportFlags")]
    public uint flags;

    public VkExternalSemaphoreHandleTypeFlagBits handleType;

    public int fd;
}
