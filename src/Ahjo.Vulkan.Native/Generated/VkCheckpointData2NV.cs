namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCheckpointData2NV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkPipelineStageFlags2")]
    public ulong stage;

    public void* pCheckpointMarker;
}
