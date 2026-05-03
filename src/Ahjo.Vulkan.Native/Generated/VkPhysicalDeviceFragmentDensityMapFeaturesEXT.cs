namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFragmentDensityMapFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint fragmentDensityMap;

    [NativeTypeName("VkBool32")]
    public uint fragmentDensityMapDynamic;

    [NativeTypeName("VkBool32")]
    public uint fragmentDensityMapNonSubsampledImages;
}
