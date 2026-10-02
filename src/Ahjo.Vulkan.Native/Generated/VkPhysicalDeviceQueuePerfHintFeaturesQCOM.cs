namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceQueuePerfHintFeaturesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint queuePerfHint;
}
