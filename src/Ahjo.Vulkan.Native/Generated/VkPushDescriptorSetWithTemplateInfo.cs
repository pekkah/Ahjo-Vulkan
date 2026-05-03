namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPushDescriptorSetWithTemplateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDescriptorUpdateTemplate")]
    public VkDescriptorUpdateTemplate_T* descriptorUpdateTemplate;

    [NativeTypeName("VkPipelineLayout")]
    public VkPipelineLayout_T* layout;

    [NativeTypeName("uint32_t")]
    public uint set;

    [NativeTypeName("const void *")]
    public void* pData;
}
