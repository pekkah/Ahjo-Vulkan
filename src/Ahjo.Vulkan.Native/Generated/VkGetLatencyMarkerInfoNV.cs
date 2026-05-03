namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGetLatencyMarkerInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint timingCount;

    public VkLatencyTimingsFrameReportNV* pTimings;
}
