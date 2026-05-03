namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceTileShadingPropertiesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxApronSize;

    [NativeTypeName("VkBool32")]
    public uint preferNonCoherent;

    public VkExtent2D tileGranularity;

    public VkExtent2D maxTileShadingRate;
}
