namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCooperativeMatrixProperties2EXT
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
}
