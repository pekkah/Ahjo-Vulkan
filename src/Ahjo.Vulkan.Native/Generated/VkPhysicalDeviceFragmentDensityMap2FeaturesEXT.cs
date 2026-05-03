namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFragmentDensityMap2FeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint fragmentDensityMapDeferred;
}
