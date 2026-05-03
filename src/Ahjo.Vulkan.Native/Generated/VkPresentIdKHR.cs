namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPresentIdKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint swapchainCount;

    [NativeTypeName("const uint64_t *")]
    public ulong* pPresentIds;
}
