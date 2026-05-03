namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPresentRegionsKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint swapchainCount;

    [NativeTypeName("const VkPresentRegionKHR *")]
    public VkPresentRegionKHR* pRegions;
}
