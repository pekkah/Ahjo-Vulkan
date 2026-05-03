namespace Ahjo.Vulkan.Native;

public partial struct VkImageSubresourceLayers
{
    [NativeTypeName("VkImageAspectFlags")]
    public uint aspectMask;

    [NativeTypeName("uint32_t")]
    public uint mipLevel;

    [NativeTypeName("uint32_t")]
    public uint baseArrayLayer;

    [NativeTypeName("uint32_t")]
    public uint layerCount;
}
