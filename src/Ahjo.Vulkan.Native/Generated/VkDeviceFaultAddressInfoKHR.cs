namespace Ahjo.Vulkan.Native;

public partial struct VkDeviceFaultAddressInfoKHR
{
    public VkDeviceFaultAddressTypeKHR addressType;

    [NativeTypeName("VkDeviceAddress")]
    public ulong reportedAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong addressPrecision;
}
