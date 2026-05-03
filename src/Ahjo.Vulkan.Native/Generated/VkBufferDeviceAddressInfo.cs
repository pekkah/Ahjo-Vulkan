namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBufferDeviceAddressInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* buffer;
}
