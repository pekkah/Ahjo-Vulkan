namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBindHeapInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDeviceAddressRangeEXT heapRange;

    [NativeTypeName("VkDeviceSize")]
    public ulong reservedRangeOffset;

    [NativeTypeName("VkDeviceSize")]
    public ulong reservedRangeSize;
}
