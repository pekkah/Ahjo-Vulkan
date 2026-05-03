namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkQueueFamilyCheckpointPropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkPipelineStageFlags")]
    public uint checkpointExecutionStageMask;
}
