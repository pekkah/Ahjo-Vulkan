namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBufferOpaqueCaptureAddressCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong opaqueCaptureAddress;
}
