namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageStencilUsageCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImageUsageFlags")]
    public uint stencilUsage;
}
