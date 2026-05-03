namespace Ahjo.Vulkan.Native;

public partial struct VkStridedDeviceAddressRegionKHR
{
    [NativeTypeName("VkDeviceAddress")]
    public ulong deviceAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong stride;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;
}
