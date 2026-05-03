namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkIndirectCommandsLayoutTokenNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkIndirectCommandsTokenTypeNV tokenType;

    [NativeTypeName("uint32_t")]
    public uint stream;

    [NativeTypeName("uint32_t")]
    public uint offset;

    [NativeTypeName("uint32_t")]
    public uint vertexBindingUnit;

    [NativeTypeName("VkBool32")]
    public uint vertexDynamicStride;

    [NativeTypeName("VkPipelineLayout")]
    public VkPipelineLayout_T* pushconstantPipelineLayout;

    [NativeTypeName("VkShaderStageFlags")]
    public uint pushconstantShaderStageFlags;

    [NativeTypeName("uint32_t")]
    public uint pushconstantOffset;

    [NativeTypeName("uint32_t")]
    public uint pushconstantSize;

    [NativeTypeName("VkIndirectStateFlagsNV")]
    public uint indirectStateFlags;

    [NativeTypeName("uint32_t")]
    public uint indexTypeCount;

    [NativeTypeName("const VkIndexType *")]
    public VkIndexType* pIndexTypes;

    [NativeTypeName("const uint32_t *")]
    public uint* pIndexTypeValues;
}
