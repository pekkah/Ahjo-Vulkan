namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFragmentDensityMap2PropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint subsampledLoads;

    [NativeTypeName("VkBool32")]
    public uint subsampledCoarseReconstructionEarlyAccess;

    [NativeTypeName("uint32_t")]
    public uint maxSubsampledArrayLayers;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetSubsampledSamplers;
}
