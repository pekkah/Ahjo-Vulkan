namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeUsageInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkVideoEncodeUsageFlagsKHR")]
    public uint videoUsageHints;

    [NativeTypeName("VkVideoEncodeContentFlagsKHR")]
    public uint videoContentHints;

    public VkVideoEncodeTuningModeKHR tuningMode;
}
