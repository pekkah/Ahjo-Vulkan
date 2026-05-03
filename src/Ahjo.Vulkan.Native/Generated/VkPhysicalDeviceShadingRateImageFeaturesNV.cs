namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShadingRateImageFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shadingRateImage;

    [NativeTypeName("VkBool32")]
    public uint shadingRateCoarseSampleOrder;
}
