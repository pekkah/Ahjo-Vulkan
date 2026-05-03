namespace Ahjo.Vulkan.Native;

public unsafe partial struct StdVideoH264SequenceParameterSetVui
{
    public StdVideoH264SpsVuiFlags flags;

    public StdVideoH264AspectRatioIdc aspect_ratio_idc;

    [NativeTypeName("uint16_t")]
    public ushort sar_width;

    [NativeTypeName("uint16_t")]
    public ushort sar_height;

    [NativeTypeName("uint8_t")]
    public byte video_format;

    [NativeTypeName("uint8_t")]
    public byte colour_primaries;

    [NativeTypeName("uint8_t")]
    public byte transfer_characteristics;

    [NativeTypeName("uint8_t")]
    public byte matrix_coefficients;

    [NativeTypeName("uint32_t")]
    public uint num_units_in_tick;

    [NativeTypeName("uint32_t")]
    public uint time_scale;

    [NativeTypeName("uint8_t")]
    public byte max_num_reorder_frames;

    [NativeTypeName("uint8_t")]
    public byte max_dec_frame_buffering;

    [NativeTypeName("uint8_t")]
    public byte chroma_sample_loc_type_top_field;

    [NativeTypeName("uint8_t")]
    public byte chroma_sample_loc_type_bottom_field;

    [NativeTypeName("uint32_t")]
    public uint reserved1;

    [NativeTypeName("const StdVideoH264HrdParameters *")]
    public StdVideoH264HrdParameters* pHrdParameters;
}
