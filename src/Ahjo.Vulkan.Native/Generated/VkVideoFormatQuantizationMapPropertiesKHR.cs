namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoFormatQuantizationMapPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    public VkExtent2D quantizationMapTexelSize;
}
