namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageStencilUsage2CreateInfoKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkImageUsageFlags2KHR")]
    public ulong stencilUsage;
}
