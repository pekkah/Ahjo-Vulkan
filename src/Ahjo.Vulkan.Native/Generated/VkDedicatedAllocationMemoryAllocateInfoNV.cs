namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDedicatedAllocationMemoryAllocateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImage")]
    public VkImage_T* image;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* buffer;
}
