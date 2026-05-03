namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageDrmFormatModifierPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong drmFormatModifier;
}
