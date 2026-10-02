namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkThrottleHintSubmitInfoSEC
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkThrottleHintTypeSEC throttleHint;
}
