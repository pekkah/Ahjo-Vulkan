namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBufferMemoryRequirementsInfo2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* buffer;
}
