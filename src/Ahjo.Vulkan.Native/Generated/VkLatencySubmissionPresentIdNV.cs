namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkLatencySubmissionPresentIdNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong presentID;
}
