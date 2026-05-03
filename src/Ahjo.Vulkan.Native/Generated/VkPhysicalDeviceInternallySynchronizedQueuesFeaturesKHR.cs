namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceInternallySynchronizedQueuesFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint internallySynchronizedQueues;
}
