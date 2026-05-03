namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceSubgroupProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint subgroupSize;

    [NativeTypeName("VkShaderStageFlags")]
    public uint supportedStages;

    [NativeTypeName("VkSubgroupFeatureFlags")]
    public uint supportedOperations;

    [NativeTypeName("VkBool32")]
    public uint quadOperationsInAllStages;
}
