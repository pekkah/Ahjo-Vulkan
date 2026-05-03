using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

[StructLayout(LayoutKind.Explicit)]
public partial struct VkAccelerationStructureMotionInstanceDataNV
{
    [FieldOffset(0)]
    public VkAccelerationStructureInstanceKHR staticInstance;

    [FieldOffset(0)]
    public VkAccelerationStructureMatrixMotionInstanceNV matrixMotionInstance;

    [FieldOffset(0)]
    public VkAccelerationStructureSRTMotionInstanceNV srtMotionInstance;
}
