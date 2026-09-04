namespace Ahjo.Vulkan.Ngx.Native;

public unsafe partial struct NVSDK_NGX_ImageViewInfo_VK
{
    [NativeTypeName("VkImageView")]
    public Ahjo.Vulkan.Native.VkImageView_T* ImageView;

    [NativeTypeName("VkImage")]
    public Ahjo.Vulkan.Native.VkImage_T* Image;

    [NativeTypeName("VkImageSubresourceRange")]
    public Ahjo.Vulkan.Native.VkImageSubresourceRange SubresourceRange;

    [NativeTypeName("VkFormat")]
    public Ahjo.Vulkan.Native.VkFormat Format;

    [NativeTypeName("unsigned int")]
    public uint Width;

    [NativeTypeName("unsigned int")]
    public uint Height;
}
