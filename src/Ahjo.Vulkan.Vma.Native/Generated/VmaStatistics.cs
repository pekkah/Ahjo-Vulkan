namespace Ahjo.Vulkan.Vma.Native;

public partial struct VmaStatistics
{
    [NativeTypeName("uint32_t")]
    public uint blockCount;

    [NativeTypeName("uint32_t")]
    public uint allocationCount;

    [NativeTypeName("VkDeviceSize")]
    public ulong blockBytes;

    [NativeTypeName("VkDeviceSize")]
    public ulong allocationBytes;
}
