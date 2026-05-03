namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkIndirectCommandsLayoutCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkIndirectCommandsLayoutUsageFlagsEXT")]
    public uint flags;

    [NativeTypeName("VkShaderStageFlags")]
    public uint shaderStages;

    [NativeTypeName("uint32_t")]
    public uint indirectStride;

    [NativeTypeName("VkPipelineLayout")]
    public VkPipelineLayout_T* pipelineLayout;

    [NativeTypeName("uint32_t")]
    public uint tokenCount;

    [NativeTypeName("const VkIndirectCommandsLayoutTokenEXT *")]
    public VkIndirectCommandsLayoutTokenEXT* pTokens;
}
