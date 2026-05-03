namespace Ahjo.Vulkan.Native;

public partial struct StdVideoEncodeAV1OperatingPointInfo
{
    public StdVideoEncodeAV1OperatingPointInfoFlags flags;

    [NativeTypeName("uint16_t")]
    public ushort operating_point_idc;

    [NativeTypeName("uint8_t")]
    public byte seq_level_idx;

    [NativeTypeName("uint8_t")]
    public byte seq_tier;

    [NativeTypeName("uint32_t")]
    public uint decoder_buffer_delay;

    [NativeTypeName("uint32_t")]
    public uint encoder_buffer_delay;

    [NativeTypeName("uint8_t")]
    public byte initial_display_delay_minus_1;
}
