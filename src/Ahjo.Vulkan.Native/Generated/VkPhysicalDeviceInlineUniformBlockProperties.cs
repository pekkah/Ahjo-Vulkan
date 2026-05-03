namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceInlineUniformBlockProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxInlineUniformBlockSize;

    [NativeTypeName("uint32_t")]
    public uint maxPerStageDescriptorInlineUniformBlocks;

    [NativeTypeName("uint32_t")]
    public uint maxPerStageDescriptorUpdateAfterBindInlineUniformBlocks;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetInlineUniformBlocks;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetUpdateAfterBindInlineUniformBlocks;
}
