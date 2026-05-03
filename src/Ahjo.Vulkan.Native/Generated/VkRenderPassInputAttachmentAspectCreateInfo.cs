namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassInputAttachmentAspectCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint aspectReferenceCount;

    [NativeTypeName("const VkInputAttachmentAspectReference *")]
    public VkInputAttachmentAspectReference* pAspectReferences;
}
