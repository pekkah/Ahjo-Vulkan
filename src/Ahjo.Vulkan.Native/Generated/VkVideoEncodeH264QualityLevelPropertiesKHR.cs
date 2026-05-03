namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH264QualityLevelPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkVideoEncodeH264RateControlFlagsKHR")]
    public uint preferredRateControlFlags;

    [NativeTypeName("uint32_t")]
    public uint preferredGopFrameCount;

    [NativeTypeName("uint32_t")]
    public uint preferredIdrPeriod;

    [NativeTypeName("uint32_t")]
    public uint preferredConsecutiveBFrameCount;

    [NativeTypeName("uint32_t")]
    public uint preferredTemporalLayerCount;

    public VkVideoEncodeH264QpKHR preferredConstantQp;

    [NativeTypeName("uint32_t")]
    public uint preferredMaxL0ReferenceCount;

    [NativeTypeName("uint32_t")]
    public uint preferredMaxL1ReferenceCount;

    [NativeTypeName("VkBool32")]
    public uint preferredStdEntropyCodingModeFlag;
}
