namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderMultipleWaitQueuesPropertiesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxShaderWaitQueues;
}
