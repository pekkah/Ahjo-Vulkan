namespace Ahjo.Vulkan.Vma.Native;

public unsafe partial struct VmaVirtualBlockCreateInfo
{
    [NativeTypeName("VkDeviceSize")]
    public ulong size;

    [NativeTypeName("VmaVirtualBlockCreateFlags")]
    public uint flags;

    [NativeTypeName("const VkAllocationCallbacks * _Nullable")]
    public Ahjo.Vulkan.Native.VkAllocationCallbacks* pAllocationCallbacks;
}
