namespace Ahjo.Vulkan.Native;

public partial struct VkDecompressMemoryRegionEXT
{
    [NativeTypeName("VkDeviceAddress")]
    public ulong srcAddress;

    [NativeTypeName("VkDeviceAddress")]
    public ulong dstAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong compressedSize;

    [NativeTypeName("VkDeviceSize")]
    public ulong decompressedSize;
}
