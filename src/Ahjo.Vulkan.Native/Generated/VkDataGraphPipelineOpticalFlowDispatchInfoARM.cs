namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineOpticalFlowDispatchInfoARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDataGraphOpticalFlowExecuteFlagsARM")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint meanFlowL1NormHint;
}
