using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoAV1Segmentation
{
    [NativeTypeName("uint8_t[8]")]
    public _FeatureEnabled_e__FixedBuffer FeatureEnabled;

    [NativeTypeName("int16_t[8][8]")]
    public _FeatureData_e__FixedBuffer FeatureData;

    [InlineArray(8)]
    public partial struct _FeatureEnabled_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8 * 8)]
    public partial struct _FeatureData_e__FixedBuffer
    {
        public short e0_0;
    }
}
