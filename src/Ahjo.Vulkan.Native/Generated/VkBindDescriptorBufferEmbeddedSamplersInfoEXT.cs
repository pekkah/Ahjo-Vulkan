namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBindDescriptorBufferEmbeddedSamplersInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkShaderStageFlags")]
    public uint stageFlags;

    [NativeTypeName("VkPipelineLayout")]
    public VkPipelineLayout_T* layout;

    [NativeTypeName("uint32_t")]
    public uint set;
}
