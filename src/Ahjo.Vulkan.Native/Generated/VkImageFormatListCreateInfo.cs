namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageFormatListCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint viewFormatCount;

    [NativeTypeName("const VkFormat *")]
    public VkFormat* pViewFormats;
}
