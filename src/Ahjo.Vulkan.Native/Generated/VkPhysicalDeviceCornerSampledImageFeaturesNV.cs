namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCornerSampledImageFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint cornerSampledImage;
}
