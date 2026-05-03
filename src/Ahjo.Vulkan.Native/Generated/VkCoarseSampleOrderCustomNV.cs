namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCoarseSampleOrderCustomNV
{
    public VkShadingRatePaletteEntryNV shadingRate;

    [NativeTypeName("uint32_t")]
    public uint sampleCount;

    [NativeTypeName("uint32_t")]
    public uint sampleLocationCount;

    [NativeTypeName("const VkCoarseSampleLocationNV *")]
    public VkCoarseSampleLocationNV* pSampleLocations;
}
