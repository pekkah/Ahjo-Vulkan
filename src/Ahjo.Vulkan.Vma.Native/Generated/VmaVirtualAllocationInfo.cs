namespace Ahjo.Vulkan.Vma.Native;

public unsafe partial struct VmaVirtualAllocationInfo
{
    [NativeTypeName("VkDeviceSize")]
    public ulong offset;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;

    [NativeTypeName("void * _Nullable")]
    public void* pUserData;
}
