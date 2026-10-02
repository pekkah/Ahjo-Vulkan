namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeFeedback2CapabilitiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxPerPartitionFeedbackEntries;

    [NativeTypeName("VkVideoEncodePerPartitionFeedbackFlagsKHR")]
    public uint supportedPerPartitionEncodeFeedbackFlags;
}
