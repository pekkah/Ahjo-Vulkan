namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorSetVariableDescriptorCountLayoutSupport
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxVariableDescriptorCount;
}
