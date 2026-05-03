namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphProcessingEngineCreateInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint processingEngineCount;

    public VkPhysicalDeviceDataGraphProcessingEngineARM* pProcessingEngines;
}
