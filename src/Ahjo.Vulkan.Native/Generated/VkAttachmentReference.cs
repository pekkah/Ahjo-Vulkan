namespace Ahjo.Vulkan.Native;

public partial struct VkAttachmentReference
{
    [NativeTypeName("uint32_t")]
    public uint attachment;

    public VkImageLayout layout;
}
