namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineResourceInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint descriptorSet;

    [NativeTypeName("uint32_t")]
    public uint binding;

    [NativeTypeName("uint32_t")]
    public uint arrayElement;
}
