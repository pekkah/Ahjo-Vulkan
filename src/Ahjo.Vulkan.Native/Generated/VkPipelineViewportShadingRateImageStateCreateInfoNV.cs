namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineViewportShadingRateImageStateCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shadingRateImageEnable;

    [NativeTypeName("uint32_t")]
    public uint viewportCount;

    [NativeTypeName("const VkShadingRatePaletteNV *")]
    public VkShadingRatePaletteNV* pShadingRatePalettes;
}
