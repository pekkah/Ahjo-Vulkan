namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorSetAllocateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDescriptorPool")]
    public VkDescriptorPool_T* descriptorPool;

    [NativeTypeName("uint32_t")]
    public uint descriptorSetCount;

    [NativeTypeName("const VkDescriptorSetLayout *")]
    public VkDescriptorSetLayout_T** pSetLayouts;
}
