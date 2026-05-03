namespace Ahjo.Vulkan.Native;

public partial struct VkImageSubresource
{
    [NativeTypeName("VkImageAspectFlags")]
    public uint aspectMask;

    [NativeTypeName("uint32_t")]
    public uint mipLevel;

    [NativeTypeName("uint32_t")]
    public uint arrayLayer;
}
