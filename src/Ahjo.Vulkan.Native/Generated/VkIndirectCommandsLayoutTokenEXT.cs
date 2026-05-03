namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkIndirectCommandsLayoutTokenEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkIndirectCommandsTokenTypeEXT type;

    public VkIndirectCommandsTokenDataEXT data;

    [NativeTypeName("uint32_t")]
    public uint offset;
}
