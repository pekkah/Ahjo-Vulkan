namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBindImageMemoryDeviceGroupInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint deviceIndexCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pDeviceIndices;

    [NativeTypeName("uint32_t")]
    public uint splitInstanceBindRegionCount;

    [NativeTypeName("const VkRect2D *")]
    public VkRect2D* pSplitInstanceBindRegions;
}
