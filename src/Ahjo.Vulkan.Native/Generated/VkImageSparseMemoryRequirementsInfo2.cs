namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageSparseMemoryRequirementsInfo2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImage")]
    public VkImage_T* image;
}
