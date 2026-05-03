namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImageCreateFlags")]
    public uint flags;

    public VkImageType imageType;

    public VkFormat format;

    public VkExtent3D extent;

    [NativeTypeName("uint32_t")]
    public uint mipLevels;

    [NativeTypeName("uint32_t")]
    public uint arrayLayers;

    public VkSampleCountFlagBits samples;

    public VkImageTiling tiling;

    [NativeTypeName("VkImageUsageFlags")]
    public uint usage;

    public VkSharingMode sharingMode;

    [NativeTypeName("uint32_t")]
    public uint queueFamilyIndexCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pQueueFamilyIndices;

    public VkImageLayout initialLayout;
}
