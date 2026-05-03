namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceUniformBufferStandardLayoutFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint uniformBufferStandardLayout;
}
