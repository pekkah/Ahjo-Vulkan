namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkShaderCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkShaderCreateFlagsEXT")]
    public uint flags;

    public VkShaderStageFlagBits stage;

    [NativeTypeName("VkShaderStageFlags")]
    public uint nextStage;

    public VkShaderCodeTypeEXT codeType;

    [NativeTypeName("size_t")]
    public nuint codeSize;

    [NativeTypeName("const void *")]
    public void* pCode;

    [NativeTypeName("const char *")]
    public sbyte* pName;

    [NativeTypeName("uint32_t")]
    public uint setLayoutCount;

    [NativeTypeName("const VkDescriptorSetLayout *")]
    public VkDescriptorSetLayout_T** pSetLayouts;

    [NativeTypeName("uint32_t")]
    public uint pushConstantRangeCount;

    [NativeTypeName("const VkPushConstantRange *")]
    public VkPushConstantRange* pPushConstantRanges;

    [NativeTypeName("const VkSpecializationInfo *")]
    public VkSpecializationInfo* pSpecializationInfo;
}
