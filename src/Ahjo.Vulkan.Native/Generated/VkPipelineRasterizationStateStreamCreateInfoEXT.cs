namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineRasterizationStateStreamCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineRasterizationStateStreamCreateFlagsEXT")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint rasterizationStream;
}
