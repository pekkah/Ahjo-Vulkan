namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPresentTimingInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPresentTimingInfoFlagsEXT")]
    public uint flags;

    [NativeTypeName("uint64_t")]
    public ulong targetTime;

    [NativeTypeName("uint64_t")]
    public ulong timeDomainId;

    [NativeTypeName("VkPresentStageFlagsEXT")]
    public uint presentStageQueries;

    [NativeTypeName("VkPresentStageFlagsEXT")]
    public uint targetTimeDomainPresentStage;
}
