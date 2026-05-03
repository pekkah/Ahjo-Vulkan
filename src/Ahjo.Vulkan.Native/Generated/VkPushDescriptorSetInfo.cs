namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPushDescriptorSetInfo
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

    [NativeTypeName("uint32_t")]
    public uint descriptorWriteCount;

    [NativeTypeName("const VkWriteDescriptorSet *")]
    public VkWriteDescriptorSet* pDescriptorWrites;
}
