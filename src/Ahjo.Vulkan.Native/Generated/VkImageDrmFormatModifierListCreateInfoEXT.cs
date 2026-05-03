namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageDrmFormatModifierListCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint drmFormatModifierCount;

    [NativeTypeName("const uint64_t *")]
    public ulong* pDrmFormatModifiers;
}
