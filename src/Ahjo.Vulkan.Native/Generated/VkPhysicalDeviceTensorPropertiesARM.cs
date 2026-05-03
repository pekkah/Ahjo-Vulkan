namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceTensorPropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxTensorDimensionCount;

    [NativeTypeName("uint64_t")]
    public ulong maxTensorElements;

    [NativeTypeName("uint64_t")]
    public ulong maxPerDimensionTensorElements;

    [NativeTypeName("int64_t")]
    public long maxTensorStride;

    [NativeTypeName("uint64_t")]
    public ulong maxTensorSize;

    [NativeTypeName("uint32_t")]
    public uint maxTensorShaderAccessArrayLength;

    [NativeTypeName("uint32_t")]
    public uint maxTensorShaderAccessSize;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetStorageTensors;

    [NativeTypeName("uint32_t")]
    public uint maxPerStageDescriptorSetStorageTensors;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetUpdateAfterBindStorageTensors;

    [NativeTypeName("uint32_t")]
    public uint maxPerStageDescriptorUpdateAfterBindStorageTensors;

    [NativeTypeName("VkBool32")]
    public uint shaderStorageTensorArrayNonUniformIndexingNative;

    [NativeTypeName("VkShaderStageFlags")]
    public uint shaderTensorSupportedStages;
}
