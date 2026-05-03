namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSwapchainCalibratedTimestampInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSwapchainKHR")]
    public VkSwapchainKHR_T* swapchain;

    [NativeTypeName("VkPresentStageFlagsEXT")]
    public uint presentStage;

    [NativeTypeName("uint64_t")]
    public ulong timeDomainId;
}
