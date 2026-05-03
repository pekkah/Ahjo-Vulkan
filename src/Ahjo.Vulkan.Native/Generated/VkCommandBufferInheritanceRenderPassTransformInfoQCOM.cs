namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCommandBufferInheritanceRenderPassTransformInfoQCOM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkSurfaceTransformFlagBitsKHR transform;

    public VkRect2D renderArea;
}
