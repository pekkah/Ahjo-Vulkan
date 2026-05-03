namespace Ahjo.Vulkan.Native;

public partial struct StdVideoEncodeAV1DecoderModelInfo
{
    [NativeTypeName("uint8_t")]
    public byte buffer_delay_length_minus_1;

    [NativeTypeName("uint8_t")]
    public byte buffer_removal_time_length_minus_1;

    [NativeTypeName("uint8_t")]
    public byte frame_presentation_time_length_minus_1;

    [NativeTypeName("uint8_t")]
    public byte reserved1;

    [NativeTypeName("uint32_t")]
    public uint num_units_in_decoding_tick;
}
