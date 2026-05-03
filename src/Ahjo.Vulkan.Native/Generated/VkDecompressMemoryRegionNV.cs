namespace Ahjo.Vulkan.Native;

public partial struct VkDecompressMemoryRegionNV
{
    [NativeTypeName("VkDeviceAddress")]
    public ulong srcAddress;

    [NativeTypeName("VkDeviceAddress")]
    public ulong dstAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong compressedSize;

    [NativeTypeName("VkDeviceSize")]
    public ulong decompressedSize;

    [NativeTypeName("VkMemoryDecompressionMethodFlagsNV")]
    public ulong decompressionMethod;
}
