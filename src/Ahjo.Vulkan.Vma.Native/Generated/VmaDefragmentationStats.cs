namespace Ahjo.Vulkan.Vma.Native;

public partial struct VmaDefragmentationStats
{
    [NativeTypeName("VkDeviceSize")]
    public ulong bytesMoved;

    [NativeTypeName("VkDeviceSize")]
    public ulong bytesFreed;

    [NativeTypeName("uint32_t")]
    public uint allocationsMoved;

    [NativeTypeName("uint32_t")]
    public uint deviceMemoryBlocksFreed;
}
