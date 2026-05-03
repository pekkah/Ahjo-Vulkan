namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBufferCopy2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong srcOffset;

    [NativeTypeName("VkDeviceSize")]
    public ulong dstOffset;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;
}
