namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceQueueFamilyDataGraphProcessingEngineInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint queueFamilyIndex;

    public VkPhysicalDeviceDataGraphProcessingEngineTypeARM engineType;
}
