namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassMultiviewCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint subpassCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pViewMasks;

    [NativeTypeName("uint32_t")]
    public uint dependencyCount;

    [NativeTypeName("const int32_t *")]
    public int* pViewOffsets;

    [NativeTypeName("uint32_t")]
    public uint correlationMaskCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pCorrelationMasks;
}
