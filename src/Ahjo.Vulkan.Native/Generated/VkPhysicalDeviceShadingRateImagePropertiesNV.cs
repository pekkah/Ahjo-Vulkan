namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShadingRateImagePropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    public VkExtent2D shadingRateTexelSize;

    [NativeTypeName("uint32_t")]
    public uint shadingRatePaletteSize;

    [NativeTypeName("uint32_t")]
    public uint shadingRateMaxCoarseSamples;
}
