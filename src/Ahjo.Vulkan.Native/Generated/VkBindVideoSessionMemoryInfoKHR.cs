namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBindVideoSessionMemoryInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint memoryBindIndex;

    [NativeTypeName("VkDeviceMemory")]
    public VkDeviceMemory_T* memory;

    [NativeTypeName("VkDeviceSize")]
    public ulong memoryOffset;

    [NativeTypeName("VkDeviceSize")]
    public ulong memorySize;
}
