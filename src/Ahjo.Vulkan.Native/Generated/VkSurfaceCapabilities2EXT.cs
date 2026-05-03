namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSurfaceCapabilities2EXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint minImageCount;

    [NativeTypeName("uint32_t")]
    public uint maxImageCount;

    public VkExtent2D currentExtent;

    public VkExtent2D minImageExtent;

    public VkExtent2D maxImageExtent;

    [NativeTypeName("uint32_t")]
    public uint maxImageArrayLayers;

    [NativeTypeName("VkSurfaceTransformFlagsKHR")]
    public uint supportedTransforms;

    public VkSurfaceTransformFlagBitsKHR currentTransform;

    [NativeTypeName("VkCompositeAlphaFlagsKHR")]
    public uint supportedCompositeAlpha;

    [NativeTypeName("VkImageUsageFlags")]
    public uint supportedUsageFlags;

    [NativeTypeName("VkSurfaceCounterFlagsEXT")]
    public uint supportedSurfaceCounters;
}
