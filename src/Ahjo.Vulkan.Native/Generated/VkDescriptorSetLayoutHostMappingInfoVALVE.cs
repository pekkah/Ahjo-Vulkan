namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorSetLayoutHostMappingInfoVALVE
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("size_t")]
    public nuint descriptorOffset;

    [NativeTypeName("uint32_t")]
    public uint descriptorSize;
}
