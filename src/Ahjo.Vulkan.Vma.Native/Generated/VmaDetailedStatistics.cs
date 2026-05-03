namespace Ahjo.Vulkan.Vma.Native;

public partial struct VmaDetailedStatistics
{
    public VmaStatistics statistics;

    [NativeTypeName("uint32_t")]
    public uint unusedRangeCount;

    [NativeTypeName("VkDeviceSize")]
    public ulong allocationSizeMin;

    [NativeTypeName("VkDeviceSize")]
    public ulong allocationSizeMax;

    [NativeTypeName("VkDeviceSize")]
    public ulong unusedRangeSizeMin;

    [NativeTypeName("VkDeviceSize")]
    public ulong unusedRangeSizeMax;
}
