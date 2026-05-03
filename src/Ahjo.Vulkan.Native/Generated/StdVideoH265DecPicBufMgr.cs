using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoH265DecPicBufMgr
{
    [NativeTypeName("uint32_t[7]")]
    public _max_latency_increase_plus1_e__FixedBuffer max_latency_increase_plus1;

    [NativeTypeName("uint8_t[7]")]
    public _max_dec_pic_buffering_minus1_e__FixedBuffer max_dec_pic_buffering_minus1;

    [NativeTypeName("uint8_t[7]")]
    public _max_num_reorder_pics_e__FixedBuffer max_num_reorder_pics;

    [InlineArray(7)]
    public partial struct _max_latency_increase_plus1_e__FixedBuffer
    {
        public uint e0;
    }

    [InlineArray(7)]
    public partial struct _max_dec_pic_buffering_minus1_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(7)]
    public partial struct _max_num_reorder_pics_e__FixedBuffer
    {
        public byte e0;
    }
}
