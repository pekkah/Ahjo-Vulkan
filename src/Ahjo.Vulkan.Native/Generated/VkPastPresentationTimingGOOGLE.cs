namespace Ahjo.Vulkan.Native;

public partial struct VkPastPresentationTimingGOOGLE
{
    [NativeTypeName("uint32_t")]
    public uint presentID;

    [NativeTypeName("uint64_t")]
    public ulong desiredPresentTime;

    [NativeTypeName("uint64_t")]
    public ulong actualPresentTime;

    [NativeTypeName("uint64_t")]
    public ulong earliestPresentTime;

    [NativeTypeName("uint64_t")]
    public ulong presentMargin;
}
