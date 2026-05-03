namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRasterizationOrderAttachmentAccessFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint rasterizationOrderColorAttachmentAccess;

    [NativeTypeName("VkBool32")]
    public uint rasterizationOrderDepthAttachmentAccess;

    [NativeTypeName("VkBool32")]
    public uint rasterizationOrderStencilAttachmentAccess;
}
