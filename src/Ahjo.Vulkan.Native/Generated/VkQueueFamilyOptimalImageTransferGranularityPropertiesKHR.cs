namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkQueueFamilyOptimalImageTransferGranularityPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    public VkExtent3D optimalImageTransferGranularity;
}
