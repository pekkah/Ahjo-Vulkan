namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImportMemoryFdInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkExternalMemoryHandleTypeFlagBits handleType;

    public int fd;
}
