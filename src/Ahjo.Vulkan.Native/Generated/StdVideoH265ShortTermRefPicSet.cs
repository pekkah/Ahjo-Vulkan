using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoH265ShortTermRefPicSet
{
    public StdVideoH265ShortTermRefPicSetFlags flags;

    [NativeTypeName("uint32_t")]
    public uint delta_idx_minus1;

    [NativeTypeName("uint16_t")]
    public ushort use_delta_flag;

    [NativeTypeName("uint16_t")]
    public ushort abs_delta_rps_minus1;

    [NativeTypeName("uint16_t")]
    public ushort used_by_curr_pic_flag;

    [NativeTypeName("uint16_t")]
    public ushort used_by_curr_pic_s0_flag;

    [NativeTypeName("uint16_t")]
    public ushort used_by_curr_pic_s1_flag;

    [NativeTypeName("uint16_t")]
    public ushort reserved1;

    [NativeTypeName("uint8_t")]
    public byte reserved2;

    [NativeTypeName("uint8_t")]
    public byte reserved3;

    [NativeTypeName("uint8_t")]
    public byte num_negative_pics;

    [NativeTypeName("uint8_t")]
    public byte num_positive_pics;

    [NativeTypeName("uint16_t[16]")]
    public _delta_poc_s0_minus1_e__FixedBuffer delta_poc_s0_minus1;

    [NativeTypeName("uint16_t[16]")]
    public _delta_poc_s1_minus1_e__FixedBuffer delta_poc_s1_minus1;

    [InlineArray(16)]
    public partial struct _delta_poc_s0_minus1_e__FixedBuffer
    {
        public ushort e0;
    }

    [InlineArray(16)]
    public partial struct _delta_poc_s1_minus1_e__FixedBuffer
    {
        public ushort e0;
    }
}
