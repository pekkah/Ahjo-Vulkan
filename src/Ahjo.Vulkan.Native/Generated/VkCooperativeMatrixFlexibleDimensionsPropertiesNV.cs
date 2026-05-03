namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCooperativeMatrixFlexibleDimensionsPropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint MGranularity;

    [NativeTypeName("uint32_t")]
    public uint NGranularity;

    [NativeTypeName("uint32_t")]
    public uint KGranularity;

    public VkComponentTypeKHR AType;

    public VkComponentTypeKHR BType;

    public VkComponentTypeKHR CType;

    public VkComponentTypeKHR ResultType;

    [NativeTypeName("VkBool32")]
    public uint saturatingAccumulation;

    public VkScopeKHR scope;

    [NativeTypeName("uint32_t")]
    public uint workgroupInvocations;
}
