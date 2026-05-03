namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineViewportWScalingStateCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint viewportWScalingEnable;

    [NativeTypeName("uint32_t")]
    public uint viewportCount;

    [NativeTypeName("const VkViewportWScalingNV *")]
    public VkViewportWScalingNV* pViewportWScalings;
}
