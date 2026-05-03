namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAcquireNextImageInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSwapchainKHR")]
    public VkSwapchainKHR_T* swapchain;

    [NativeTypeName("uint64_t")]
    public ulong timeout;

    [NativeTypeName("VkSemaphore")]
    public VkSemaphore_T* semaphore;

    [NativeTypeName("VkFence")]
    public VkFence_T* fence;

    [NativeTypeName("uint32_t")]
    public uint deviceMask;
}
