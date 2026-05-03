namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPresentTimesInfoGOOGLE
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint swapchainCount;

    [NativeTypeName("const VkPresentTimeGOOGLE *")]
    public VkPresentTimeGOOGLE* pTimes;
}
