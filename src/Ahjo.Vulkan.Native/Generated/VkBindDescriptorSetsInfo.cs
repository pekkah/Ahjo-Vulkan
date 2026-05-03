namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBindDescriptorSetsInfo
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
    public uint descriptorSetCount;

    [NativeTypeName("const VkDescriptorSet *")]
    public VkDescriptorSet_T** pDescriptorSets;

    [NativeTypeName("uint32_t")]
    public uint dynamicOffsetCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pDynamicOffsets;
}
