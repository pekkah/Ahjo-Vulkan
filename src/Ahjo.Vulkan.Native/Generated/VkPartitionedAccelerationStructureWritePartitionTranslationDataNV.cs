using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct VkPartitionedAccelerationStructureWritePartitionTranslationDataNV
{
    [NativeTypeName("uint32_t")]
    public uint partitionIndex;

    [NativeTypeName("float[3]")]
    public _partitionTranslation_e__FixedBuffer partitionTranslation;

    [InlineArray(3)]
    public partial struct _partitionTranslation_e__FixedBuffer
    {
        public float e0;
    }
}
