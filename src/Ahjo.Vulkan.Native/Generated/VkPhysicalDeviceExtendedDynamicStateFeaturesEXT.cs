namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceExtendedDynamicStateFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint extendedDynamicState;
}
