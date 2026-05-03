namespace Ahjo.Vulkan.Vma.Native;

public partial struct VmaBudget
{
    public VmaStatistics statistics;

    [NativeTypeName("VkDeviceSize")]
    public ulong usage;

    [NativeTypeName("VkDeviceSize")]
    public ulong budget;
}
