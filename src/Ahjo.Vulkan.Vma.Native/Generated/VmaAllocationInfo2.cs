namespace Ahjo.Vulkan.Vma.Native;

public partial struct VmaAllocationInfo2
{
    public VmaAllocationInfo allocationInfo;

    [NativeTypeName("VkDeviceSize")]
    public ulong blockSize;

    [NativeTypeName("VkBool32")]
    public uint dedicatedMemory;
}
