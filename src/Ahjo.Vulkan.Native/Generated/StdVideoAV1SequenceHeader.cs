using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct StdVideoAV1SequenceHeader
{
    public StdVideoAV1SequenceHeaderFlags flags;

    public StdVideoAV1Profile seq_profile;

    [NativeTypeName("uint8_t")]
    public byte frame_width_bits_minus_1;

    [NativeTypeName("uint8_t")]
    public byte frame_height_bits_minus_1;

    [NativeTypeName("uint16_t")]
    public ushort max_frame_width_minus_1;

    [NativeTypeName("uint16_t")]
    public ushort max_frame_height_minus_1;

    [NativeTypeName("uint8_t")]
    public byte delta_frame_id_length_minus_2;

    [NativeTypeName("uint8_t")]
    public byte additional_frame_id_length_minus_1;

    [NativeTypeName("uint8_t")]
    public byte order_hint_bits_minus_1;

    [NativeTypeName("uint8_t")]
    public byte seq_force_integer_mv;

    [NativeTypeName("uint8_t")]
    public byte seq_force_screen_content_tools;

    [NativeTypeName("uint8_t[5]")]
    public _reserved1_e__FixedBuffer reserved1;

    [NativeTypeName("const StdVideoAV1ColorConfig *")]
    public StdVideoAV1ColorConfig* pColorConfig;

    [NativeTypeName("const StdVideoAV1TimingInfo *")]
    public StdVideoAV1TimingInfo* pTimingInfo;

    [InlineArray(5)]
    public partial struct _reserved1_e__FixedBuffer
    {
        public byte e0;
    }
}
