namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMemoryMapInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkMemoryMapFlags")]
    public uint flags;

    [NativeTypeName("VkDeviceMemory")]
    public VkDeviceMemory_T* memory;

    [NativeTypeName("VkDeviceSize")]
    public ulong offset;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;
}
