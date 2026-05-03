namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkWriteDescriptorSet
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDescriptorSet")]
    public VkDescriptorSet_T* dstSet;

    [NativeTypeName("uint32_t")]
    public uint dstBinding;

    [NativeTypeName("uint32_t")]
    public uint dstArrayElement;

    [NativeTypeName("uint32_t")]
    public uint descriptorCount;

    public VkDescriptorType descriptorType;

    [NativeTypeName("const VkDescriptorImageInfo *")]
    public VkDescriptorImageInfo* pImageInfo;

    [NativeTypeName("const VkDescriptorBufferInfo *")]
    public VkDescriptorBufferInfo* pBufferInfo;

    [NativeTypeName("const VkBufferView *")]
    public VkBufferView_T** pTexelBufferView;
}
