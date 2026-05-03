using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoEncodeH265WeightTable
{
    public StdVideoEncodeH265WeightTableFlags flags;

    [NativeTypeName("uint8_t")]
    public byte luma_log2_weight_denom;

    [NativeTypeName("int8_t")]
    public sbyte delta_chroma_log2_weight_denom;

    [NativeTypeName("int8_t[15]")]
    public _delta_luma_weight_l0_e__FixedBuffer delta_luma_weight_l0;

    [NativeTypeName("int8_t[15]")]
    public _luma_offset_l0_e__FixedBuffer luma_offset_l0;

    [NativeTypeName("int8_t[15][2]")]
    public _delta_chroma_weight_l0_e__FixedBuffer delta_chroma_weight_l0;

    [NativeTypeName("int8_t[15][2]")]
    public _delta_chroma_offset_l0_e__FixedBuffer delta_chroma_offset_l0;

    [NativeTypeName("int8_t[15]")]
    public _delta_luma_weight_l1_e__FixedBuffer delta_luma_weight_l1;

    [NativeTypeName("int8_t[15]")]
    public _luma_offset_l1_e__FixedBuffer luma_offset_l1;

    [NativeTypeName("int8_t[15][2]")]
    public _delta_chroma_weight_l1_e__FixedBuffer delta_chroma_weight_l1;

    [NativeTypeName("int8_t[15][2]")]
    public _delta_chroma_offset_l1_e__FixedBuffer delta_chroma_offset_l1;

    [InlineArray(15)]
    public partial struct _delta_luma_weight_l0_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(15)]
    public partial struct _luma_offset_l0_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(15 * 2)]
    public partial struct _delta_chroma_weight_l0_e__FixedBuffer
    {
        public sbyte e0_0;
    }

    [InlineArray(15 * 2)]
    public partial struct _delta_chroma_offset_l0_e__FixedBuffer
    {
        public sbyte e0_0;
    }

    [InlineArray(15)]
    public partial struct _delta_luma_weight_l1_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(15)]
    public partial struct _luma_offset_l1_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(15 * 2)]
    public partial struct _delta_chroma_weight_l1_e__FixedBuffer
    {
        public sbyte e0_0;
    }

    [InlineArray(15 * 2)]
    public partial struct _delta_chroma_offset_l1_e__FixedBuffer
    {
        public sbyte e0_0;
    }
}
