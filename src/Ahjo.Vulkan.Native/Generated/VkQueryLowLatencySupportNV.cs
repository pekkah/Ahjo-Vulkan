namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkQueryLowLatencySupportNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public void* pQueriedLowLatencyData;
}
