namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSwapchainPresentModesCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint presentModeCount;

    [NativeTypeName("const VkPresentModeKHR *")]
    public VkPresentModeKHR* pPresentModes;
}
