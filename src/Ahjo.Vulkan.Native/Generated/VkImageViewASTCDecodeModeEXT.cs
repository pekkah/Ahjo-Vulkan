namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageViewASTCDecodeModeEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkFormat decodeMode;
}
