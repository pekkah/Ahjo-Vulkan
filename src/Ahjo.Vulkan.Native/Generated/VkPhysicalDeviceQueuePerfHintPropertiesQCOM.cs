namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceQueuePerfHintPropertiesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkQueueFlags")]
    public uint supportedQueues;
}
