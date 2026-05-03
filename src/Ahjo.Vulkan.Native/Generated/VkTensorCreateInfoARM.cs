namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkTensorCreateInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkTensorCreateFlagsARM")]
    public ulong flags;

    [NativeTypeName("const VkTensorDescriptionARM *")]
    public VkTensorDescriptionARM* pDescription;

    public VkSharingMode sharingMode;

    [NativeTypeName("uint32_t")]
    public uint queueFamilyIndexCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pQueueFamilyIndices;
}
