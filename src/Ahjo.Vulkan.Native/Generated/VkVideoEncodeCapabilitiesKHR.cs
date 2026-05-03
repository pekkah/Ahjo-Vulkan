namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeCapabilitiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkVideoEncodeCapabilityFlagsKHR")]
    public uint flags;

    [NativeTypeName("VkVideoEncodeRateControlModeFlagsKHR")]
    public uint rateControlModes;

    [NativeTypeName("uint32_t")]
    public uint maxRateControlLayers;

    [NativeTypeName("uint64_t")]
    public ulong maxBitrate;

    [NativeTypeName("uint32_t")]
    public uint maxQualityLevels;

    public VkExtent2D encodeInputPictureGranularity;

    [NativeTypeName("VkVideoEncodeFeedbackFlagsKHR")]
    public uint supportedEncodeFeedbackFlags;
}
