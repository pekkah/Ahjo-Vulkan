using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoH265SubLayerHrdParameters
{
    [NativeTypeName("uint32_t[32]")]
    public _bit_rate_value_minus1_e__FixedBuffer bit_rate_value_minus1;

    [NativeTypeName("uint32_t[32]")]
    public _cpb_size_value_minus1_e__FixedBuffer cpb_size_value_minus1;

    [NativeTypeName("uint32_t[32]")]
    public _cpb_size_du_value_minus1_e__FixedBuffer cpb_size_du_value_minus1;

    [NativeTypeName("uint32_t[32]")]
    public _bit_rate_du_value_minus1_e__FixedBuffer bit_rate_du_value_minus1;

    [NativeTypeName("uint32_t")]
    public uint cbr_flag;

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
    public partial struct _cpb_size_du_value_minus1_e__FixedBuffer
    {
        public uint e0;
    }

    [InlineArray(32)]
    public partial struct _bit_rate_du_value_minus1_e__FixedBuffer
    {
        public uint e0;
    }
}
