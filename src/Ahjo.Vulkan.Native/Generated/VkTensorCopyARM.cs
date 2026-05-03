namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkTensorCopyARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint dimensionCount;

    [NativeTypeName("const uint64_t *")]
    public ulong* pSrcOffset;

    [NativeTypeName("const uint64_t *")]
    public ulong* pDstOffset;

    [NativeTypeName("const uint64_t *")]
    public ulong* pExtent;
}
