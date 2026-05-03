namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceExternalComputeQueuePropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint externalDataSize;

    [NativeTypeName("uint32_t")]
    public uint maxExternalQueues;
}
