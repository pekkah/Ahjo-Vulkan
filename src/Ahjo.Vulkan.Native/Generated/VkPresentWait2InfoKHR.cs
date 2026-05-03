namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPresentWait2InfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint64_t")]
    public ulong presentId;

    [NativeTypeName("uint64_t")]
    public ulong timeout;
}
