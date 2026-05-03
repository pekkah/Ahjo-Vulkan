namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkIndirectCommandsLayoutPushDataTokenNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint pushDataOffset;

    [NativeTypeName("uint32_t")]
    public uint pushDataSize;
}
