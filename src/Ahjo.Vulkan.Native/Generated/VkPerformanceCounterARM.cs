namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPerformanceCounterARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint counterID;
}
