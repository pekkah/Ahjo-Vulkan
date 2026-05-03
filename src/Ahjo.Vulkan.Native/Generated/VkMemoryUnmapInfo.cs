namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMemoryUnmapInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkMemoryUnmapFlags")]
    public uint flags;

    [NativeTypeName("VkDeviceMemory")]
    public VkDeviceMemory_T* memory;
}
