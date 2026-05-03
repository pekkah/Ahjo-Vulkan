namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPastPresentationTimingInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPastPresentationTimingFlagsEXT")]
    public uint flags;

    [NativeTypeName("VkSwapchainKHR")]
    public VkSwapchainKHR_T* swapchain;
}
