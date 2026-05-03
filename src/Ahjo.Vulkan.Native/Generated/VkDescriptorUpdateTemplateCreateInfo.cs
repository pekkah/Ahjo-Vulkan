namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorUpdateTemplateCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDescriptorUpdateTemplateCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint descriptorUpdateEntryCount;

    [NativeTypeName("const VkDescriptorUpdateTemplateEntry *")]
    public VkDescriptorUpdateTemplateEntry* pDescriptorUpdateEntries;

    public VkDescriptorUpdateTemplateType templateType;

    [NativeTypeName("VkDescriptorSetLayout")]
    public VkDescriptorSetLayout_T* descriptorSetLayout;

    public VkPipelineBindPoint pipelineBindPoint;

    [NativeTypeName("VkPipelineLayout")]
    public VkPipelineLayout_T* pipelineLayout;

    [NativeTypeName("uint32_t")]
    public uint set;
}
