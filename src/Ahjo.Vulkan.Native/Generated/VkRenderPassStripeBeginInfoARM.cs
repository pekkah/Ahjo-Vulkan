namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassStripeBeginInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint stripeInfoCount;

    [NativeTypeName("const VkRenderPassStripeInfoARM *")]
    public VkRenderPassStripeInfoARM* pStripeInfos;
}
