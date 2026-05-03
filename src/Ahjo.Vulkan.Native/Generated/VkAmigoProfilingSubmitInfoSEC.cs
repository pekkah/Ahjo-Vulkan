namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAmigoProfilingSubmitInfoSEC
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong firstDrawTimestamp;

    [NativeTypeName("uint64_t")]
    public ulong swapBufferTimestamp;
}
