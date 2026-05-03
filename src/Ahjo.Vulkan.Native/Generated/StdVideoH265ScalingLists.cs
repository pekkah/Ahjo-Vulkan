using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoH265ScalingLists
{
    [NativeTypeName("uint8_t[6][16]")]
    public _ScalingList4x4_e__FixedBuffer ScalingList4x4;

    [NativeTypeName("uint8_t[6][64]")]
    public _ScalingList8x8_e__FixedBuffer ScalingList8x8;

    [NativeTypeName("uint8_t[6][64]")]
    public _ScalingList16x16_e__FixedBuffer ScalingList16x16;

    [NativeTypeName("uint8_t[2][64]")]
    public _ScalingList32x32_e__FixedBuffer ScalingList32x32;

    [NativeTypeName("uint8_t[6]")]
    public _ScalingListDCCoef16x16_e__FixedBuffer ScalingListDCCoef16x16;

    [NativeTypeName("uint8_t[2]")]
    public _ScalingListDCCoef32x32_e__FixedBuffer ScalingListDCCoef32x32;

    [InlineArray(6 * 16)]
    public partial struct _ScalingList4x4_e__FixedBuffer
    {
        public byte e0_0;
    }

    [InlineArray(6 * 64)]
    public partial struct _ScalingList8x8_e__FixedBuffer
    {
        public byte e0_0;
    }

    [InlineArray(6 * 64)]
    public partial struct _ScalingList16x16_e__FixedBuffer
    {
        public byte e0_0;
    }

    [InlineArray(2 * 64)]
    public partial struct _ScalingList32x32_e__FixedBuffer
    {
        public byte e0_0;
    }

    [InlineArray(6)]
    public partial struct _ScalingListDCCoef16x16_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(2)]
    public partial struct _ScalingListDCCoef32x32_e__FixedBuffer
    {
        public byte e0;
    }
}
