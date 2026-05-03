namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPastPresentationTimingEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong presentId;

    [NativeTypeName("uint64_t")]
    public ulong targetTime;

    [NativeTypeName("uint32_t")]
    public uint presentStageCount;

    public VkPresentStageTimeEXT* pPresentStages;

    public VkTimeDomainKHR timeDomain;

    [NativeTypeName("uint64_t")]
    public ulong timeDomainId;

    [NativeTypeName("VkBool32")]
    public uint reportComplete;
}
