namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePipelineLibraryGroupHandlesFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint pipelineLibraryGroupHandles;
}
