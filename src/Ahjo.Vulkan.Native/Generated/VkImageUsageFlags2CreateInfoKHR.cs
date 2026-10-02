namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageUsageFlags2CreateInfoKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkImageUsageFlags2KHR")]
    public ulong usage;
}
