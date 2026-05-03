namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkOpticalFlowExecuteInfoNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkOpticalFlowExecuteFlagsNV")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint regionCount;

    [NativeTypeName("const VkRect2D *")]
    public VkRect2D* pRegions;
}
