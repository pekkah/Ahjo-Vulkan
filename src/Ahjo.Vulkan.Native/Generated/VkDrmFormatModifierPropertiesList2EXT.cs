namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDrmFormatModifierPropertiesList2EXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint drmFormatModifierCount;

    public VkDrmFormatModifierProperties2EXT* pDrmFormatModifierProperties;
}
