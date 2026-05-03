namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassStripeInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkRect2D stripeArea;
}
