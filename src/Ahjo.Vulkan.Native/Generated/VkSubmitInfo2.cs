namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSubmitInfo2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSubmitFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint waitSemaphoreInfoCount;

    [NativeTypeName("const VkSemaphoreSubmitInfo *")]
    public VkSemaphoreSubmitInfo* pWaitSemaphoreInfos;

    [NativeTypeName("uint32_t")]
    public uint commandBufferInfoCount;

    [NativeTypeName("const VkCommandBufferSubmitInfo *")]
    public VkCommandBufferSubmitInfo* pCommandBufferInfos;

    [NativeTypeName("uint32_t")]
    public uint signalSemaphoreInfoCount;

    [NativeTypeName("const VkSemaphoreSubmitInfo *")]
    public VkSemaphoreSubmitInfo* pSignalSemaphoreInfos;
}
