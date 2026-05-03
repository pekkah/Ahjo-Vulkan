namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDeviceGeneratedCommandsFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint deviceGeneratedCommands;
}
