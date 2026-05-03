namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassFragmentDensityMapOffsetEndInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint fragmentDensityOffsetCount;

    [NativeTypeName("const VkOffset2D *")]
    public VkOffset2D* pFragmentDensityOffsets;
}
