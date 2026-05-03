namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCopyMemoryIndirectPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkQueueFlags")]
    public uint supportedQueues;
}
