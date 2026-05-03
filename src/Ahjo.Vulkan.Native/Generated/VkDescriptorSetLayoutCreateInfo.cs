namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorSetLayoutCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDescriptorSetLayoutCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint bindingCount;

    [NativeTypeName("const VkDescriptorSetLayoutBinding *")]
    public VkDescriptorSetLayoutBinding* pBindings;
}
