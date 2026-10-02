namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDispatchParametersARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint workGroupBatchSize;

    [NativeTypeName("uint32_t")]
    public uint maxQueuedWorkGroupBatches;

    [NativeTypeName("uint32_t")]
    public uint maxWarpsPerShaderCore;
}
