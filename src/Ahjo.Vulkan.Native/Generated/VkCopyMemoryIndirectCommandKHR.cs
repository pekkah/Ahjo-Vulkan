namespace Ahjo.Vulkan.Native;

public partial struct VkCopyMemoryIndirectCommandKHR
{
    [NativeTypeName("VkDeviceAddress")]
    public ulong srcAddress;

    [NativeTypeName("VkDeviceAddress")]
    public ulong dstAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;
}
