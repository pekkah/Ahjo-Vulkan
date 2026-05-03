using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoAV1LoopFilter
{
    public StdVideoAV1LoopFilterFlags flags;

    [NativeTypeName("uint8_t[4]")]
    public _loop_filter_level_e__FixedBuffer loop_filter_level;

    [NativeTypeName("uint8_t")]
    public byte loop_filter_sharpness;

    [NativeTypeName("uint8_t")]
    public byte update_ref_delta;

    [NativeTypeName("int8_t[8]")]
    public _loop_filter_ref_deltas_e__FixedBuffer loop_filter_ref_deltas;

    [NativeTypeName("uint8_t")]
    public byte update_mode_delta;

    [NativeTypeName("int8_t[2]")]
    public _loop_filter_mode_deltas_e__FixedBuffer loop_filter_mode_deltas;

    [InlineArray(4)]
    public partial struct _loop_filter_level_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8)]
    public partial struct _loop_filter_ref_deltas_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(2)]
    public partial struct _loop_filter_mode_deltas_e__FixedBuffer
    {
        public sbyte e0;
    }
}
