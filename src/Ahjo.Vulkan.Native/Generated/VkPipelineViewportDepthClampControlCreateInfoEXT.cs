namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineViewportDepthClampControlCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDepthClampModeEXT depthClampMode;

    [NativeTypeName("const VkDepthClampRangeEXT *")]
    public VkDepthClampRangeEXT* pDepthClampRange;
}
