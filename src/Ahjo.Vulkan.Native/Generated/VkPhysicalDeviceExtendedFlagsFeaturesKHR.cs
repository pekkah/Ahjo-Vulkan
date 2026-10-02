namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceExtendedFlagsFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint extendedFlags;
}
