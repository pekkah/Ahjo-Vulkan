namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBufferViewCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBufferViewCreateFlags")]
    public uint flags;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* buffer;

    public VkFormat format;

    [NativeTypeName("VkDeviceSize")]
    public ulong offset;

    [NativeTypeName("VkDeviceSize")]
    public ulong range;
}
