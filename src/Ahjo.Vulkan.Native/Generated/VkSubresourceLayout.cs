namespace Ahjo.Vulkan.Native;

public partial struct VkSubresourceLayout
{
    [NativeTypeName("VkDeviceSize")]
    public ulong offset;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;

    [NativeTypeName("VkDeviceSize")]
    public ulong rowPitch;

    [NativeTypeName("VkDeviceSize")]
    public ulong arrayPitch;

    [NativeTypeName("VkDeviceSize")]
    public ulong depthPitch;
}
