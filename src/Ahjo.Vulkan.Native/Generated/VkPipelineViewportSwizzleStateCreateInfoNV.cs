namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineViewportSwizzleStateCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineViewportSwizzleStateCreateFlagsNV")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint viewportCount;

    [NativeTypeName("const VkViewportSwizzleNV *")]
    public VkViewportSwizzleNV* pViewportSwizzles;
}
