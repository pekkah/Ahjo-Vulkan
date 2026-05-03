namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFragmentDensityMapLayeredFeaturesVALVE
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint fragmentDensityMapLayered;
}
