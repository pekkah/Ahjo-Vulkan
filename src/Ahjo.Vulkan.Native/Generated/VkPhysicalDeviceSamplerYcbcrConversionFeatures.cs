namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceSamplerYcbcrConversionFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint samplerYcbcrConversion;
}
