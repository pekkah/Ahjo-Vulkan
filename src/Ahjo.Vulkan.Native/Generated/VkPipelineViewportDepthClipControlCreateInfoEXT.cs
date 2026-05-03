namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineViewportDepthClipControlCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint negativeOneToOne;
}
