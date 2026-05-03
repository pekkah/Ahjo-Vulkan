namespace Ahjo.Vulkan.Native;

public unsafe partial struct StdVideoH265VideoParameterSet
{
    public StdVideoH265VpsFlags flags;

    [NativeTypeName("uint8_t")]
    public byte vps_video_parameter_set_id;

    [NativeTypeName("uint8_t")]
    public byte vps_max_sub_layers_minus1;

    [NativeTypeName("uint8_t")]
    public byte reserved1;

    [NativeTypeName("uint8_t")]
    public byte reserved2;

    [NativeTypeName("uint32_t")]
    public uint vps_num_units_in_tick;

    [NativeTypeName("uint32_t")]
    public uint vps_time_scale;

    [NativeTypeName("uint32_t")]
    public uint vps_num_ticks_poc_diff_one_minus1;

    [NativeTypeName("uint32_t")]
    public uint reserved3;

    [NativeTypeName("const StdVideoH265DecPicBufMgr *")]
    public StdVideoH265DecPicBufMgr* pDecPicBufMgr;

    [NativeTypeName("const StdVideoH265HrdParameters *")]
    public StdVideoH265HrdParameters* pHrdParameters;

    [NativeTypeName("const StdVideoH265ProfileTierLevel *")]
    public StdVideoH265ProfileTierLevel* pProfileTierLevel;
}
