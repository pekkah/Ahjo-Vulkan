using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoH264ScalingLists
{
    [NativeTypeName("uint16_t")]
    public ushort scaling_list_present_mask;

    [NativeTypeName("uint16_t")]
    public ushort use_default_scaling_matrix_mask;

    [NativeTypeName("uint8_t[6][16]")]
    public _ScalingList4x4_e__FixedBuffer ScalingList4x4;

    [NativeTypeName("uint8_t[6][64]")]
    public _ScalingList8x8_e__FixedBuffer ScalingList8x8;

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
}
