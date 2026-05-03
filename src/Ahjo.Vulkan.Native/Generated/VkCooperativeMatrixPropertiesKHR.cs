namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCooperativeMatrixPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint MSize;

    [NativeTypeName("uint32_t")]
    public uint NSize;

    [NativeTypeName("uint32_t")]
    public uint KSize;

    public VkComponentTypeKHR AType;

    public VkComponentTypeKHR BType;

    public VkComponentTypeKHR CType;

    public VkComponentTypeKHR ResultType;

    [NativeTypeName("VkBool32")]
    public uint saturatingAccumulation;

    public VkScopeKHR scope;
}
