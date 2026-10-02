namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePipelineLibraryGroupHandlesFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint pipelineLibraryGroupHandles;
}
