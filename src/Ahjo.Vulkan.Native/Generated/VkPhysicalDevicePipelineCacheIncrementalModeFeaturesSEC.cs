namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePipelineCacheIncrementalModeFeaturesSEC
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint pipelineCacheIncrementalMode;
}
