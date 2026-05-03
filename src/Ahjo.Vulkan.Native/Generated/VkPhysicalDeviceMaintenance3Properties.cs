namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMaintenance3Properties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxPerSetDescriptors;

    [NativeTypeName("VkDeviceSize")]
    public ulong maxMemoryAllocationSize;
}
