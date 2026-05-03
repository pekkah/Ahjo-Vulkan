namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeQualityLevelPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    public VkVideoEncodeRateControlModeFlagBitsKHR preferredRateControlMode;

    [NativeTypeName("uint32_t")]
    public uint preferredRateControlLayerCount;
}
