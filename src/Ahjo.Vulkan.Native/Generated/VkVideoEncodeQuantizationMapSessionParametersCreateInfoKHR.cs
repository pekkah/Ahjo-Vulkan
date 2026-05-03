namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeQuantizationMapSessionParametersCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkExtent2D quantizationMapTexelSize;
}
