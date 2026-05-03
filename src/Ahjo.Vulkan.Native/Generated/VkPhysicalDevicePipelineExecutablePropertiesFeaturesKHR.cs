namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePipelineExecutablePropertiesFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint pipelineExecutableInfo;
}
