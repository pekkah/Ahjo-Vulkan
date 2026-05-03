namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineShaderStageCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineShaderStageCreateFlags")]
    public uint flags;

    public VkShaderStageFlagBits stage;

    [NativeTypeName("VkShaderModule")]
    public VkShaderModule_T* module;

    [NativeTypeName("const char *")]
    public sbyte* pName;

    [NativeTypeName("const VkSpecializationInfo *")]
    public VkSpecializationInfo* pSpecializationInfo;
}
