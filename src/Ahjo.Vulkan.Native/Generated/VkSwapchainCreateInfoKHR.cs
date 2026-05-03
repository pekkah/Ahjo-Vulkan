namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSwapchainCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSwapchainCreateFlagsKHR")]
    public uint flags;

    [NativeTypeName("VkSurfaceKHR")]
    public VkSurfaceKHR_T* surface;

    [NativeTypeName("uint32_t")]
    public uint minImageCount;

    public VkFormat imageFormat;

    public VkColorSpaceKHR imageColorSpace;

    public VkExtent2D imageExtent;

    [NativeTypeName("uint32_t")]
    public uint imageArrayLayers;

    [NativeTypeName("VkImageUsageFlags")]
    public uint imageUsage;

    public VkSharingMode imageSharingMode;

    [NativeTypeName("uint32_t")]
    public uint queueFamilyIndexCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pQueueFamilyIndices;

    public VkSurfaceTransformFlagBitsKHR preTransform;

    public VkCompositeAlphaFlagBitsKHR compositeAlpha;

    public VkPresentModeKHR presentMode;

    [NativeTypeName("VkBool32")]
    public uint clipped;

    [NativeTypeName("VkSwapchainKHR")]
    public VkSwapchainKHR_T* oldSwapchain;
}
