namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageViewCaptureDescriptorDataInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImageView")]
    public VkImageView_T* imageView;
}
