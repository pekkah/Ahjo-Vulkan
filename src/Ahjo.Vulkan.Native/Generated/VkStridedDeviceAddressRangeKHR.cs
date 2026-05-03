namespace Ahjo.Vulkan.Native;

public partial struct VkStridedDeviceAddressRangeKHR
{
    [NativeTypeName("VkDeviceAddress")]
    public ulong address;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;

    [NativeTypeName("VkDeviceSize")]
    public ulong stride;
}
