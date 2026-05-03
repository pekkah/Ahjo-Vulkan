namespace Ahjo.Vulkan.Native;

public partial struct VkAccelerationStructureMotionInstanceNV
{
    public VkAccelerationStructureMotionInstanceTypeNV type;

    [NativeTypeName("VkAccelerationStructureMotionInstanceFlagsNV")]
    public uint flags;

    public VkAccelerationStructureMotionInstanceDataNV data;
}
