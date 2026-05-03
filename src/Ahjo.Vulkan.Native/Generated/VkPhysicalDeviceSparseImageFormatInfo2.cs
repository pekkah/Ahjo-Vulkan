namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceSparseImageFormatInfo2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkFormat format;

    public VkImageType type;

    public VkSampleCountFlagBits samples;

    [NativeTypeName("VkImageUsageFlags")]
    public uint usage;

    public VkImageTiling tiling;
}
