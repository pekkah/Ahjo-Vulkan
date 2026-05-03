namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeQuantizationMapInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImageView")]
    public VkImageView_T* quantizationMap;

    public VkExtent2D quantizationMapExtent;
}
