namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceSchedulingControlsDispatchParametersPropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint schedulingControlsMaxWarpsCount;

    [NativeTypeName("uint32_t")]
    public uint schedulingControlsMaxQueuedBatchesCount;

    [NativeTypeName("uint32_t")]
    public uint schedulingControlsMaxWorkGroupBatchSize;
}
