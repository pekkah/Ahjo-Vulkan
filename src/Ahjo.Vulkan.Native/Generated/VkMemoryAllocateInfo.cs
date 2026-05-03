namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMemoryAllocateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong allocationSize;

    [NativeTypeName("uint32_t")]
    public uint memoryTypeIndex;
}
