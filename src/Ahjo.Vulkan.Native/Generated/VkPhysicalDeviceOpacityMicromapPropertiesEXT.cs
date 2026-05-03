namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceOpacityMicromapPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxOpacity2StateSubdivisionLevel;

    [NativeTypeName("uint32_t")]
    public uint maxOpacity4StateSubdivisionLevel;
}
