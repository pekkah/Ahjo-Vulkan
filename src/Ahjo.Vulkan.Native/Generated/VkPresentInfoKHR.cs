namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPresentInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint waitSemaphoreCount;

    [NativeTypeName("const VkSemaphore *")]
    public VkSemaphore_T** pWaitSemaphores;

    [NativeTypeName("uint32_t")]
    public uint swapchainCount;

    [NativeTypeName("const VkSwapchainKHR *")]
    public VkSwapchainKHR_T** pSwapchains;

    [NativeTypeName("const uint32_t *")]
    public uint* pImageIndices;

    public VkResult* pResults;
}
