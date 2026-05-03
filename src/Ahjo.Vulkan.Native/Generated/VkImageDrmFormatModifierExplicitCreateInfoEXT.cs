namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageDrmFormatModifierExplicitCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong drmFormatModifier;

    [NativeTypeName("uint32_t")]
    public uint drmFormatModifierPlaneCount;

    [NativeTypeName("const VkSubresourceLayout *")]
    public VkSubresourceLayout* pPlaneLayouts;
}
