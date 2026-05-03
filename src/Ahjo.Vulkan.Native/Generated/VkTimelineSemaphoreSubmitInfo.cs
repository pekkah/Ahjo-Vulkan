namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkTimelineSemaphoreSubmitInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint waitSemaphoreValueCount;

    [NativeTypeName("const uint64_t *")]
    public ulong* pWaitSemaphoreValues;

    [NativeTypeName("uint32_t")]
    public uint signalSemaphoreValueCount;

    [NativeTypeName("const uint64_t *")]
    public ulong* pSignalSemaphoreValues;
}
