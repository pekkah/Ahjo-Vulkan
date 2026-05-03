namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceExtendedDynamicState3PropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint dynamicPrimitiveTopologyUnrestricted;
}
