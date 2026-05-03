namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkFramebufferAttachmentImageInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImageCreateFlags")]
    public uint flags;

    [NativeTypeName("VkImageUsageFlags")]
    public uint usage;

    [NativeTypeName("uint32_t")]
    public uint width;

    [NativeTypeName("uint32_t")]
    public uint height;

    [NativeTypeName("uint32_t")]
    public uint layerCount;

    [NativeTypeName("uint32_t")]
    public uint viewFormatCount;

    [NativeTypeName("const VkFormat *")]
    public VkFormat* pViewFormats;
}
