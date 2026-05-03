namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineLayoutCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineLayoutCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint setLayoutCount;

    [NativeTypeName("const VkDescriptorSetLayout *")]
    public VkDescriptorSetLayout_T** pSetLayouts;

    [NativeTypeName("uint32_t")]
    public uint pushConstantRangeCount;

    [NativeTypeName("const VkPushConstantRange *")]
    public VkPushConstantRange* pPushConstantRanges;
}
