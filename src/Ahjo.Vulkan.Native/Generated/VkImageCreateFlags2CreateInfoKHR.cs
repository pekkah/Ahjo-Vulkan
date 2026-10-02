namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageCreateFlags2CreateInfoKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkImageCreateFlags2KHR")]
    public ulong flags;
}
