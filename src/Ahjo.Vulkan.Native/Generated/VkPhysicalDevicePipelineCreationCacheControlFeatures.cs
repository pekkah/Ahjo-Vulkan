namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePipelineCreationCacheControlFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint pipelineCreationCacheControl;
}
