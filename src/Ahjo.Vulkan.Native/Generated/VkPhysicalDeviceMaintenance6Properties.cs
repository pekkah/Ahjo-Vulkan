namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMaintenance6Properties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint blockTexelViewCompatibleMultipleLayers;

    [NativeTypeName("uint32_t")]
    public uint maxCombinedImageSamplerDescriptorCount;

    [NativeTypeName("VkBool32")]
    public uint fragmentShadingRateClampCombinerInputs;
}
