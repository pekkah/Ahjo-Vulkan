namespace Ahjo.Vulkan.Native;

public partial struct VkDeviceAddressRangeEXT
{
    [NativeTypeName("VkDeviceAddress")]
    public ulong address;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;
}
