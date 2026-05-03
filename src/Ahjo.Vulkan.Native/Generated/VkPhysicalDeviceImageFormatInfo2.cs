namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceImageFormatInfo2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkFormat format;

    public VkImageType type;

    public VkImageTiling tiling;

    [NativeTypeName("VkImageUsageFlags")]
    public uint usage;

    [NativeTypeName("VkImageCreateFlags")]
    public uint flags;
}
