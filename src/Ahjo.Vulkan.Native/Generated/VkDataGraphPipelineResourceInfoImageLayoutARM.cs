namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineResourceInfoImageLayoutARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkImageLayout layout;
}
