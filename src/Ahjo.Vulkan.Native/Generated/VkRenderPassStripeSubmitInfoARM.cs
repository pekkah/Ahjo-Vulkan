namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassStripeSubmitInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint stripeSemaphoreInfoCount;

    [NativeTypeName("const VkSemaphoreSubmitInfo *")]
    public VkSemaphoreSubmitInfo* pStripeSemaphoreInfos;
}
