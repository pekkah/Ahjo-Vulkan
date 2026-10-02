namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSharedPresentSurfaceCapabilities2KHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkImageUsageFlags2KHR")]
    public ulong sharedPresentSupportedUsageFlags;
}
