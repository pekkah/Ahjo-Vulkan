namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassBeginInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkRenderPass")]
    public VkRenderPass_T* renderPass;

    [NativeTypeName("VkFramebuffer")]
    public VkFramebuffer_T* framebuffer;

    public VkRect2D renderArea;

    [NativeTypeName("uint32_t")]
    public uint clearValueCount;

    [NativeTypeName("const VkClearValue *")]
    public VkClearValue* pClearValues;
}
