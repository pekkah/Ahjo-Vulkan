namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceExtendedSparseAddressSpacePropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong extendedSparseAddressSpaceSize;

    [NativeTypeName("VkImageUsageFlags")]
    public uint extendedSparseImageUsageFlags;

    [NativeTypeName("VkBufferUsageFlags")]
    public uint extendedSparseBufferUsageFlags;
}
