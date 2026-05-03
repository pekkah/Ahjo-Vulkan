namespace Ahjo.Vulkan.Native;

public partial struct VkStridedDeviceAddressNV
{
    [NativeTypeName("VkDeviceAddress")]
    public ulong startAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong strideInBytes;
}
