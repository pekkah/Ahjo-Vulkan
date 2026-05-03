namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureBuildGeometryInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkAccelerationStructureTypeKHR type;

    [NativeTypeName("VkBuildAccelerationStructureFlagsKHR")]
    public uint flags;

    public VkBuildAccelerationStructureModeKHR mode;

    [NativeTypeName("VkAccelerationStructureKHR")]
    public VkAccelerationStructureKHR_T* srcAccelerationStructure;

    [NativeTypeName("VkAccelerationStructureKHR")]
    public VkAccelerationStructureKHR_T* dstAccelerationStructure;

    [NativeTypeName("uint32_t")]
    public uint geometryCount;

    [NativeTypeName("const VkAccelerationStructureGeometryKHR *")]
    public VkAccelerationStructureGeometryKHR* pGeometries;

    [NativeTypeName("const VkAccelerationStructureGeometryKHR *const *")]
    public VkAccelerationStructureGeometryKHR** ppGeometries;

    public VkDeviceOrHostAddressKHR scratchData;
}
