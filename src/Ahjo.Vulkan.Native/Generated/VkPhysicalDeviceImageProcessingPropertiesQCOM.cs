namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceImageProcessingPropertiesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxWeightFilterPhases;

    public VkExtent2D maxWeightFilterDimension;

    public VkExtent2D maxBlockMatchRegion;

    public VkExtent2D maxBoxFilterBlockSize;
}
