namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkAccelerationStructureTypeNV")]
    public VkAccelerationStructureTypeKHR type;

    [NativeTypeName("VkBuildAccelerationStructureFlagsNV")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint instanceCount;

    [NativeTypeName("uint32_t")]
    public uint geometryCount;

    [NativeTypeName("const VkGeometryNV *")]
    public VkGeometryNV* pGeometries;
}
