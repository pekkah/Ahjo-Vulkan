namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImportFenceFdInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkFence")]
    public VkFence_T* fence;

    [NativeTypeName("VkFenceImportFlags")]
    public uint flags;

    public VkExternalFenceHandleTypeFlagBits handleType;

    public int fd;
}
