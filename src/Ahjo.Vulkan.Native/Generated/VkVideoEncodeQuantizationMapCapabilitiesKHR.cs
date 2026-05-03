namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeQuantizationMapCapabilitiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    public VkExtent2D maxQuantizationMapExtent;
}
