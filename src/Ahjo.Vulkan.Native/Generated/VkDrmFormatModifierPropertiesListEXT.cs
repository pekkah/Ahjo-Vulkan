namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDrmFormatModifierPropertiesListEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint drmFormatModifierCount;

    public VkDrmFormatModifierPropertiesEXT* pDrmFormatModifierProperties;
}
