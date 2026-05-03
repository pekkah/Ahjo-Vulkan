namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceSubgroupSizeControlProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint minSubgroupSize;

    [NativeTypeName("uint32_t")]
    public uint maxSubgroupSize;

    [NativeTypeName("uint32_t")]
    public uint maxComputeWorkgroupSubgroups;

    [NativeTypeName("VkShaderStageFlags")]
    public uint requiredSubgroupSizeStages;
}
