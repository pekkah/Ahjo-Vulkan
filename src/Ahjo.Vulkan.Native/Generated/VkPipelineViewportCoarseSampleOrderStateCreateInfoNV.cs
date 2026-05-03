namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineViewportCoarseSampleOrderStateCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkCoarseSampleOrderTypeNV sampleOrderType;

    [NativeTypeName("uint32_t")]
    public uint customSampleOrderCount;

    [NativeTypeName("const VkCoarseSampleOrderCustomNV *")]
    public VkCoarseSampleOrderCustomNV* pCustomSampleOrders;
}
