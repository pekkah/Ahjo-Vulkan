namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeRateControlLayerInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong averageBitrate;

    [NativeTypeName("uint64_t")]
    public ulong maxBitrate;

    [NativeTypeName("uint32_t")]
    public uint frameRateNumerator;

    [NativeTypeName("uint32_t")]
    public uint frameRateDenominator;
}
