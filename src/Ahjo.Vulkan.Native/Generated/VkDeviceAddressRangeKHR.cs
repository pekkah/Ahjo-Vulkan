namespace Ahjo.Vulkan.Native;

public partial struct VkDeviceAddressRangeKHR
{
    [NativeTypeName("VkDeviceAddress")]
    public ulong address;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;
}
