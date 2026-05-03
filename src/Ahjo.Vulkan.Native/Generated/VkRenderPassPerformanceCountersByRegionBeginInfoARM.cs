namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassPerformanceCountersByRegionBeginInfoARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint counterAddressCount;

    [NativeTypeName("const VkDeviceAddress *")]
    public ulong* pCounterAddresses;

    [NativeTypeName("VkBool32")]
    public uint serializeRegions;

    [NativeTypeName("uint32_t")]
    public uint counterIndexCount;

    [NativeTypeName("uint32_t *")]
    public uint* pCounterIndices;
}
