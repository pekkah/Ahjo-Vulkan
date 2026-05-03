namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPerformanceMarkerInfoINTEL
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong marker;
}
