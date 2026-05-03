namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkTensorDescriptionARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkTensorTilingARM tiling;

    public VkFormat format;

    [NativeTypeName("uint32_t")]
    public uint dimensionCount;

    [NativeTypeName("const int64_t *")]
    public long* pDimensions;

    [NativeTypeName("const int64_t *")]
    public long* pStrides;

    [NativeTypeName("VkTensorUsageFlagsARM")]
    public ulong usage;
}
