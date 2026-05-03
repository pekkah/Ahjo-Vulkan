namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceAmigoProfilingFeaturesSEC
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint amigoProfiling;
}
