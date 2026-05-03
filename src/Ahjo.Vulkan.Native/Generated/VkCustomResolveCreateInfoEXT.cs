namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCustomResolveCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint customResolve;

    [NativeTypeName("uint32_t")]
    public uint colorAttachmentCount;

    [NativeTypeName("const VkFormat *")]
    public VkFormat* pColorAttachmentFormats;

    public VkFormat depthAttachmentFormat;

    public VkFormat stencilAttachmentFormat;
}
