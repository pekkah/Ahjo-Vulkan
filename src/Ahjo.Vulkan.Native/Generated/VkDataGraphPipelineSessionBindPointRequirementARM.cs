namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineSessionBindPointRequirementARM
{
    public VkStructureType sType;

    public void* pNext;

    public VkDataGraphPipelineSessionBindPointARM bindPoint;

    public VkDataGraphPipelineSessionBindPointTypeARM bindPointType;

    [NativeTypeName("uint32_t")]
    public uint numObjects;
}
