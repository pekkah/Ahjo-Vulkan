namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineViewportStateCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineViewportStateCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint viewportCount;

    [NativeTypeName("const VkViewport *")]
    public VkViewport* pViewports;

    [NativeTypeName("uint32_t")]
    public uint scissorCount;

    [NativeTypeName("const VkRect2D *")]
    public VkRect2D* pScissors;
}
