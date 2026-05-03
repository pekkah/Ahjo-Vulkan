namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSwapchainPresentFenceInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint swapchainCount;

    [NativeTypeName("const VkFence *")]
    public VkFence_T** pFences;
}
