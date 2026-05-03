namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMaintenance4Properties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong maxBufferSize;
}
