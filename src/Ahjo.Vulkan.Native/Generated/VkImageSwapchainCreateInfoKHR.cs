namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageSwapchainCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSwapchainKHR")]
    public VkSwapchainKHR_T* swapchain;
}
