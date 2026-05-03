namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCheckpointDataNV
{
    public VkStructureType sType;

    public void* pNext;

    public VkPipelineStageFlagBits stage;

    public void* pCheckpointMarker;
}
