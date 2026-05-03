namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH265QualityLevelPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkVideoEncodeH265RateControlFlagsKHR")]
    public uint preferredRateControlFlags;

    [NativeTypeName("uint32_t")]
    public uint preferredGopFrameCount;

    [NativeTypeName("uint32_t")]
    public uint preferredIdrPeriod;

    [NativeTypeName("uint32_t")]
    public uint preferredConsecutiveBFrameCount;

    [NativeTypeName("uint32_t")]
    public uint preferredSubLayerCount;

    public VkVideoEncodeH265QpKHR preferredConstantQp;

    [NativeTypeName("uint32_t")]
    public uint preferredMaxL0ReferenceCount;

    [NativeTypeName("uint32_t")]
    public uint preferredMaxL1ReferenceCount;
}
