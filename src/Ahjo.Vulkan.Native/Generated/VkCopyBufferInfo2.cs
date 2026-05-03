namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyBufferInfo2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* srcBuffer;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* dstBuffer;

    [NativeTypeName("uint32_t")]
    public uint regionCount;

    [NativeTypeName("const VkBufferCopy2 *")]
    public VkBufferCopy2* pRegions;
}
