namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderImageFootprintFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint imageFootprint;
}
