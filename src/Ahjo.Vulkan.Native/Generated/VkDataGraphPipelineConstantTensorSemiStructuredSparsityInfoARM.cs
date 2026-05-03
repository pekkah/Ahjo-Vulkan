namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineConstantTensorSemiStructuredSparsityInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint dimension;

    [NativeTypeName("uint32_t")]
    public uint zeroCount;

    [NativeTypeName("uint32_t")]
    public uint groupSize;
}
