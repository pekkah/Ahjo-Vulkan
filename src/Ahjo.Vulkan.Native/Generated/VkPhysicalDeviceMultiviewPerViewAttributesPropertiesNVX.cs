namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMultiviewPerViewAttributesPropertiesNVX
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint perViewPositionAllComponents;
}
