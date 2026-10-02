namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMemoryRangeBarriersInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint memoryRangeBarrierCount;

    [NativeTypeName("const VkMemoryRangeBarrierKHR *")]
    public VkMemoryRangeBarrierKHR* pMemoryRangeBarriers;
}
