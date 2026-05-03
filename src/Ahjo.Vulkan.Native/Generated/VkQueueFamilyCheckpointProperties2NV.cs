namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkQueueFamilyCheckpointProperties2NV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkPipelineStageFlags2")]
    public ulong checkpointExecutionStageMask;
}
