namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineSessionMemoryRequirementsInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDataGraphPipelineSessionARM")]
    public VkDataGraphPipelineSessionARM_T* session;

    public VkDataGraphPipelineSessionBindPointARM bindPoint;

    [NativeTypeName("uint32_t")]
    public uint objectIndex;
}
