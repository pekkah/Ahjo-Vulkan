namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCommandBufferInheritanceViewportScissorInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint viewportScissor2D;

    [NativeTypeName("uint32_t")]
    public uint viewportDepthCount;

    [NativeTypeName("const VkViewport *")]
    public VkViewport* pViewportDepths;
}
