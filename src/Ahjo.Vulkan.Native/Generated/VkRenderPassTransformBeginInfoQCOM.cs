namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassTransformBeginInfoQCOM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkSurfaceTransformFlagBitsKHR transform;
}
