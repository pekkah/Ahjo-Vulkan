namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorSetVariableDescriptorCountAllocateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint descriptorSetCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pDescriptorCounts;
}
