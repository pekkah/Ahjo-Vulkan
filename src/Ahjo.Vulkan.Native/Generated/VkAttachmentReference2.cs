namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAttachmentReference2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint attachment;

    public VkImageLayout layout;

    [NativeTypeName("VkImageAspectFlags")]
    public uint aspectMask;
}
