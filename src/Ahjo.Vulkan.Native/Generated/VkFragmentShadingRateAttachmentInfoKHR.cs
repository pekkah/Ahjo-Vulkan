namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkFragmentShadingRateAttachmentInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const VkAttachmentReference2 *")]
    public VkAttachmentReference2* pFragmentShadingRateAttachment;

    public VkExtent2D shadingRateAttachmentTexelSize;
}
