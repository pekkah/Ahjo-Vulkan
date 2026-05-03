using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoEncodeH265LongTermRefPics
{
    [NativeTypeName("uint8_t")]
    public byte num_long_term_sps;

    [NativeTypeName("uint8_t")]
    public byte num_long_term_pics;

    [NativeTypeName("uint8_t[32]")]
    public _lt_idx_sps_e__FixedBuffer lt_idx_sps;

    [NativeTypeName("uint8_t[16]")]
    public _poc_lsb_lt_e__FixedBuffer poc_lsb_lt;

    [NativeTypeName("uint16_t")]
    public ushort used_by_curr_pic_lt_flag;

    [NativeTypeName("uint8_t[48]")]
    public _delta_poc_msb_present_flag_e__FixedBuffer delta_poc_msb_present_flag;

    [NativeTypeName("uint8_t[48]")]
    public _delta_poc_msb_cycle_lt_e__FixedBuffer delta_poc_msb_cycle_lt;

    [InlineArray(32)]
    public partial struct _lt_idx_sps_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(16)]
    public partial struct _poc_lsb_lt_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(48)]
    public partial struct _delta_poc_msb_present_flag_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(48)]
    public partial struct _delta_poc_msb_cycle_lt_e__FixedBuffer
    {
        public byte e0;
    }
}
