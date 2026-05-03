using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct VkPartitionedAccelerationStructureWriteInstanceDataNV
{
    public VkTransformMatrixKHR transform;

    [NativeTypeName("float[6]")]
    public _explicitAABB_e__FixedBuffer explicitAABB;

    [NativeTypeName("uint32_t")]
    public uint instanceID;

    [NativeTypeName("uint32_t")]
    public uint instanceMask;

    [NativeTypeName("uint32_t")]
    public uint instanceContributionToHitGroupIndex;

    [NativeTypeName("VkPartitionedAccelerationStructureInstanceFlagsNV")]
    public uint instanceFlags;

    [NativeTypeName("uint32_t")]
    public uint instanceIndex;

    [NativeTypeName("uint32_t")]
    public uint partitionIndex;

    [NativeTypeName("VkDeviceAddress")]
    public ulong accelerationStructure;

    [InlineArray(6)]
    public partial struct _explicitAABB_e__FixedBuffer
    {
        public float e0;
    }
}
