namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkIndirectExecutionSetShaderInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint shaderCount;

    [NativeTypeName("const VkShaderEXT *")]
    public VkShaderEXT_T** pInitialShaders;

    [NativeTypeName("const VkIndirectExecutionSetShaderLayoutInfoEXT *")]
    public VkIndirectExecutionSetShaderLayoutInfoEXT* pSetLayoutInfos;

    [NativeTypeName("uint32_t")]
    public uint maxShaderCount;

    [NativeTypeName("uint32_t")]
    public uint pushConstantRangeCount;

    [NativeTypeName("const VkPushConstantRange *")]
    public VkPushConstantRange* pPushConstantRanges;
}
