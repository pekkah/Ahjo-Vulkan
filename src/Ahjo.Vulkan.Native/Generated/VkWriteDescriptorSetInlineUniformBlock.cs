namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkWriteDescriptorSetInlineUniformBlock
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint dataSize;

    [NativeTypeName("const void *")]
    public void* pData;
}
