namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceImageDrmFormatModifierInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong drmFormatModifier;

    public VkSharingMode sharingMode;

    [NativeTypeName("uint32_t")]
    public uint queueFamilyIndexCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pQueueFamilyIndices;
}
