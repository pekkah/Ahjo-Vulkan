namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeAV1RateControlInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkVideoEncodeAV1RateControlFlagsKHR")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint gopFrameCount;

    [NativeTypeName("uint32_t")]
    public uint keyFramePeriod;

    [NativeTypeName("uint32_t")]
    public uint consecutiveBipredictiveFrameCount;

    [NativeTypeName("uint32_t")]
    public uint temporalLayerCount;
}
