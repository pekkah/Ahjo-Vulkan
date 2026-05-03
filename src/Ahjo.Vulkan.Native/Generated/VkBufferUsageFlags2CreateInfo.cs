namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBufferUsageFlags2CreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBufferUsageFlags2")]
    public ulong usage;
}
