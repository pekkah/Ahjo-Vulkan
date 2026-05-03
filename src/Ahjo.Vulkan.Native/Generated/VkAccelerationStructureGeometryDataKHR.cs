using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

[StructLayout(LayoutKind.Explicit)]
public partial struct VkAccelerationStructureGeometryDataKHR
{
    [FieldOffset(0)]
    public VkAccelerationStructureGeometryTrianglesDataKHR triangles;

    [FieldOffset(0)]
    public VkAccelerationStructureGeometryAabbsDataKHR aabbs;

    [FieldOffset(0)]
    public VkAccelerationStructureGeometryInstancesDataKHR instances;
}
