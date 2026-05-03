namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderingAttachmentFlagsInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkRenderingAttachmentFlagsKHR")]
    public uint flags;
}
