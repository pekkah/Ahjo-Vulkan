namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDataGraphModelFeaturesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint dataGraphModel;
}
