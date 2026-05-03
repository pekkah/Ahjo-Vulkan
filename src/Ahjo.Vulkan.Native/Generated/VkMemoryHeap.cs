namespace Ahjo.Vulkan.Native;

public partial struct VkMemoryHeap
{
    [NativeTypeName("VkDeviceSize")]
    public ulong size;

    [NativeTypeName("VkMemoryHeapFlags")]
    public uint flags;
}
