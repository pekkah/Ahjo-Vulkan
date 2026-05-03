namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineDiscardRectangleStateCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineDiscardRectangleStateCreateFlagsEXT")]
    public uint flags;

    public VkDiscardRectangleModeEXT discardRectangleMode;

    [NativeTypeName("uint32_t")]
    public uint discardRectangleCount;

    [NativeTypeName("const VkRect2D *")]
    public VkRect2D* pDiscardRectangles;
}
