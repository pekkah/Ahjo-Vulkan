namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceOpacityMicromapPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxOpacity2StateSubdivisionLevel;

    [NativeTypeName("uint32_t")]
    public uint maxOpacity4StateSubdivisionLevel;

    [NativeTypeName("uint32_t")]
    public uint maxOpacityLossy4StateSubdivisionLevel;

    [NativeTypeName("uint64_t")]
    public ulong maxMicromapTriangles;
}
