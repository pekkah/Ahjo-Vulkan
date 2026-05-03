namespace Ahjo.Vulkan.Vma.Native;

public unsafe partial struct VmaPoolCreateInfo
{
    [NativeTypeName("uint32_t")]
    public uint memoryTypeIndex;

    [NativeTypeName("VmaPoolCreateFlags")]
    public uint flags;

    [NativeTypeName("VkDeviceSize")]
    public ulong blockSize;

    [NativeTypeName("size_t")]
    public nuint minBlockCount;

    [NativeTypeName("size_t")]
    public nuint maxBlockCount;

    public float priority;

    [NativeTypeName("VkDeviceSize")]
    public ulong minAllocationAlignment;

    [NativeTypeName("void * _Nullable")]
    public void* pMemoryAllocateNext;
}
