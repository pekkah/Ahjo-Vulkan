namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoFormatPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    public VkFormat format;

    public VkComponentMapping componentMapping;

    [NativeTypeName("VkImageCreateFlags")]
    public uint imageCreateFlags;

    public VkImageType imageType;

    public VkImageTiling imageTiling;

    [NativeTypeName("VkImageUsageFlags")]
    public uint imageUsageFlags;
}
