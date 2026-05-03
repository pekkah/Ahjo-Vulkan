namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPresentTimingsInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint swapchainCount;

    [NativeTypeName("const VkPresentTimingInfoEXT *")]
    public VkPresentTimingInfoEXT* pTimingInfos;
}
