namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBindHeapInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceAddressRangeEXT")]
    public VkDeviceAddressRangeKHR heapRange;

    [NativeTypeName("VkDeviceSize")]
    public ulong reservedRangeOffset;

    [NativeTypeName("VkDeviceSize")]
    public ulong reservedRangeSize;
}
