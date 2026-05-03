namespace Ahjo.Vulkan.Native;

public partial struct VkSparseImageFormatProperties
{
    [NativeTypeName("VkImageAspectFlags")]
    public uint aspectMask;

    public VkExtent3D imageGranularity;

    [NativeTypeName("VkSparseImageFormatFlags")]
    public uint flags;
}
