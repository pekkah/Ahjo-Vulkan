namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkLatencyTimingsFrameReportNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong presentID;

    [NativeTypeName("uint64_t")]
    public ulong inputSampleTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong simStartTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong simEndTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong renderSubmitStartTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong renderSubmitEndTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong presentStartTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong presentEndTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong driverStartTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong driverEndTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong osRenderQueueStartTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong osRenderQueueEndTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong gpuRenderStartTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong gpuRenderEndTimeUs;
}
