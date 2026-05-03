namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCooperativeVectorPropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkShaderStageFlags")]
    public uint cooperativeVectorSupportedStages;

    [NativeTypeName("VkBool32")]
    public uint cooperativeVectorTrainingFloat16Accumulation;

    [NativeTypeName("VkBool32")]
    public uint cooperativeVectorTrainingFloat32Accumulation;

    [NativeTypeName("uint32_t")]
    public uint maxCooperativeVectorComponents;
}
