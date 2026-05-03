namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureGeometryKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkGeometryTypeKHR geometryType;

    public VkAccelerationStructureGeometryDataKHR geometry;

    [NativeTypeName("VkGeometryFlagsKHR")]
    public uint flags;
}
