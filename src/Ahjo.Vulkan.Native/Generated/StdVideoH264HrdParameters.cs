using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoH264HrdParameters
{
    [NativeTypeName("uint8_t")]
    public byte cpb_cnt_minus1;

    [NativeTypeName("uint8_t")]
    public byte bit_rate_scale;

    [NativeTypeName("uint8_t")]
    public byte cpb_size_scale;

    [NativeTypeName("uint8_t")]
    public byte reserved1;

    [NativeTypeName("uint32_t[32]")]
    public _bit_rate_value_minus1_e__FixedBuffer bit_rate_value_minus1;

    [NativeTypeName("uint32_t[32]")]
    public _cpb_size_value_minus1_e__FixedBuffer cpb_size_value_minus1;

    [NativeTypeName("uint8_t[32]")]
    public _cbr_flag_e__FixedBuffer cbr_flag;

    [NativeTypeName("uint32_t")]
    public uint initial_cpb_removal_delay_length_minus1;

    [NativeTypeName("uint32_t")]
    public uint cpb_removal_delay_length_minus1;

    [NativeTypeName("uint32_t")]
    public uint dpb_output_delay_length_minus1;

    [NativeTypeName("uint32_t")]
    public uint time_offset_length;

    [InlineArray(32)]
    public partial struct _bit_rate_value_minus1_e__FixedBuffer
    {
        public uint e0;
    }

    [InlineArray(32)]
    public partial struct _cpb_size_value_minus1_e__FixedBuffer
    {
        public uint e0;
    }

    [InlineArray(32)]
    public partial struct _cbr_flag_e__FixedBuffer
    {
        public byte e0;
    }
}
