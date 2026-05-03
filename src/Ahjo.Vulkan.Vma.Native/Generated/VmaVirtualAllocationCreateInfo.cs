namespace Ahjo.Vulkan.Vma.Native;

public unsafe partial struct VmaVirtualAllocationCreateInfo
{
    [NativeTypeName("VkDeviceSize")]
    public ulong size;

    [NativeTypeName("VkDeviceSize")]
    public ulong alignment;

    [NativeTypeName("VmaVirtualAllocationCreateFlags")]
    public uint flags;

    [NativeTypeName("void * _Nullable")]
    public void* pUserData;
}
