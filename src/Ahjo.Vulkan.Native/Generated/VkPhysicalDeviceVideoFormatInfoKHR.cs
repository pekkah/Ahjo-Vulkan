namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVideoFormatInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImageUsageFlags")]
    public uint imageUsage;
}
