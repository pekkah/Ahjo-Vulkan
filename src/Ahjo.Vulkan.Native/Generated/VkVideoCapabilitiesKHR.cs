namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoCapabilitiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkVideoCapabilityFlagsKHR")]
    public uint flags;

    [NativeTypeName("VkDeviceSize")]
    public ulong minBitstreamBufferOffsetAlignment;

    [NativeTypeName("VkDeviceSize")]
    public ulong minBitstreamBufferSizeAlignment;

    public VkExtent2D pictureAccessGranularity;

    public VkExtent2D minCodedExtent;

    public VkExtent2D maxCodedExtent;

    [NativeTypeName("uint32_t")]
    public uint maxDpbSlots;

    [NativeTypeName("uint32_t")]
    public uint maxActiveReferencePictures;

    public VkExtensionProperties stdHeaderVersion;
}
