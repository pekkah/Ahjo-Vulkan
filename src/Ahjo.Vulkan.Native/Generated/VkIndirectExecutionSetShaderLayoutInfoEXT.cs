namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkIndirectExecutionSetShaderLayoutInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint setLayoutCount;

    [NativeTypeName("const VkDescriptorSetLayout *")]
    public VkDescriptorSetLayout_T** pSetLayouts;
}
