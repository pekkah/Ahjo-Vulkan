namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkQueryPoolPerformanceCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint queueFamilyIndex;

    [NativeTypeName("uint32_t")]
    public uint counterIndexCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pCounterIndices;
}
