namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePipelineOpacityMicromapFeaturesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint pipelineOpacityMicromap;
}
