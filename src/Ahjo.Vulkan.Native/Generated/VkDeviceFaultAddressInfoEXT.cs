namespace Ahjo.Vulkan.Native;

public partial struct VkDeviceFaultAddressInfoEXT
{
    public VkDeviceFaultAddressTypeEXT addressType;

    [NativeTypeName("VkDeviceAddress")]
    public ulong reportedAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong addressPrecision;
}
