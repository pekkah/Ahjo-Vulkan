namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkQueueFamilyDataGraphPropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    public VkPhysicalDeviceDataGraphProcessingEngineARM engine;

    public VkPhysicalDeviceDataGraphOperationSupportARM operation;
}
