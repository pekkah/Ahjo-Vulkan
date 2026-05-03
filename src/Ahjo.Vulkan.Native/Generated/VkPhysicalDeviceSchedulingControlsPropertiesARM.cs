namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceSchedulingControlsPropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkPhysicalDeviceSchedulingControlsFlagsARM")]
    public ulong schedulingControlsFlags;
}
