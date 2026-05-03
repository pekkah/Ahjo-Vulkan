namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCooperativeMatrixPropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint MSize;

    [NativeTypeName("uint32_t")]
    public uint NSize;

    [NativeTypeName("uint32_t")]
    public uint KSize;

    [NativeTypeName("VkComponentTypeNV")]
    public VkComponentTypeKHR AType;

    [NativeTypeName("VkComponentTypeNV")]
    public VkComponentTypeKHR BType;

    [NativeTypeName("VkComponentTypeNV")]
    public VkComponentTypeKHR CType;

    [NativeTypeName("VkComponentTypeNV")]
    public VkComponentTypeKHR DType;

    [NativeTypeName("VkScopeNV")]
    public VkScopeKHR scope;
}
