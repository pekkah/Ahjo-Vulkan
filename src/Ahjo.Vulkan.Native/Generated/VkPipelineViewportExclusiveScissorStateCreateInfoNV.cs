namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineViewportExclusiveScissorStateCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint exclusiveScissorCount;

    [NativeTypeName("const VkRect2D *")]
    public VkRect2D* pExclusiveScissors;
}
