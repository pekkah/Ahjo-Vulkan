namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePerStageDescriptorSetFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint perStageDescriptorSet;

    [NativeTypeName("VkBool32")]
    public uint dynamicPipelineLayout;
}
