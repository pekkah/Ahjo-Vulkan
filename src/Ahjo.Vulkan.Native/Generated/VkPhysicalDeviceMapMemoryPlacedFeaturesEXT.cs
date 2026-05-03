namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMapMemoryPlacedFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint memoryMapPlaced;

    [NativeTypeName("VkBool32")]
    public uint memoryMapRangePlaced;

    [NativeTypeName("VkBool32")]
    public uint memoryUnmapReserve;
}
