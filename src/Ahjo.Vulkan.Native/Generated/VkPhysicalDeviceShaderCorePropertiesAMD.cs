namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderCorePropertiesAMD
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint shaderEngineCount;

    [NativeTypeName("uint32_t")]
    public uint shaderArraysPerEngineCount;

    [NativeTypeName("uint32_t")]
    public uint computeUnitsPerShaderArray;

    [NativeTypeName("uint32_t")]
    public uint simdPerComputeUnit;

    [NativeTypeName("uint32_t")]
    public uint wavefrontsPerSimd;

    [NativeTypeName("uint32_t")]
    public uint wavefrontSize;

    [NativeTypeName("uint32_t")]
    public uint sgprsPerSimd;

    [NativeTypeName("uint32_t")]
    public uint minSgprAllocation;

    [NativeTypeName("uint32_t")]
    public uint maxSgprAllocation;

    [NativeTypeName("uint32_t")]
    public uint sgprAllocationGranularity;

    [NativeTypeName("uint32_t")]
    public uint vgprsPerSimd;

    [NativeTypeName("uint32_t")]
    public uint minVgprAllocation;

    [NativeTypeName("uint32_t")]
    public uint maxVgprAllocation;

    [NativeTypeName("uint32_t")]
    public uint vgprAllocationGranularity;
}
