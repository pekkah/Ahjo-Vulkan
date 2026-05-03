namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPastPresentationTimingPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong timingPropertiesCounter;

    [NativeTypeName("uint64_t")]
    public ulong timeDomainsCounter;

    [NativeTypeName("uint32_t")]
    public uint presentationTimingCount;

    public VkPastPresentationTimingEXT* pPresentationTimings;
}
