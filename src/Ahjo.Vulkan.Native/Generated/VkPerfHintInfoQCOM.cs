namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPerfHintInfoQCOM
{
    public VkStructureType sType;

    public void* pNext;

    public VkPerfHintTypeQCOM type;

    [NativeTypeName("uint32_t")]
    public uint scale;
}
