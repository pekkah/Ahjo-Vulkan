namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMappedMemoryRange
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceMemory")]
    public VkDeviceMemory_T* memory;

    [NativeTypeName("VkDeviceSize")]
    public ulong offset;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;
}
