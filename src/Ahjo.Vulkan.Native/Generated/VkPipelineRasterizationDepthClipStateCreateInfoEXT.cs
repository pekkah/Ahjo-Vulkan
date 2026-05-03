namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineRasterizationDepthClipStateCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineRasterizationDepthClipStateCreateFlagsEXT")]
    public uint flags;

    [NativeTypeName("VkBool32")]
    public uint depthClipEnable;
}
