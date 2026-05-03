namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSetDescriptorBufferOffsetsInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkShaderStageFlags")]
    public uint stageFlags;

    [NativeTypeName("VkPipelineLayout")]
    public VkPipelineLayout_T* layout;

    [NativeTypeName("uint32_t")]
    public uint firstSet;

    [NativeTypeName("uint32_t")]
    public uint setCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pBufferIndices;

    [NativeTypeName("const VkDeviceSize *")]
    public ulong* pOffsets;
}
