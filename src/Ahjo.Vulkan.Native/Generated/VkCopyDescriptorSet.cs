namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyDescriptorSet
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDescriptorSet")]
    public VkDescriptorSet_T* srcSet;

    [NativeTypeName("uint32_t")]
    public uint srcBinding;

    [NativeTypeName("uint32_t")]
    public uint srcArrayElement;

    [NativeTypeName("VkDescriptorSet")]
    public VkDescriptorSet_T* dstSet;

    [NativeTypeName("uint32_t")]
    public uint dstBinding;

    [NativeTypeName("uint32_t")]
    public uint dstArrayElement;

    [NativeTypeName("uint32_t")]
    public uint descriptorCount;
}
