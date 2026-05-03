namespace Ahjo.Vulkan.Native;

public partial struct VkClearAttachment
{
    [NativeTypeName("VkImageAspectFlags")]
    public uint aspectMask;

    [NativeTypeName("uint32_t")]
    public uint colorAttachment;

    public VkClearValue clearValue;
}
