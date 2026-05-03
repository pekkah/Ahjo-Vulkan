namespace Ahjo.Vulkan.Native;

public unsafe partial struct StdVideoEncodeH264SliceHeader
{
    public StdVideoEncodeH264SliceHeaderFlags flags;

    [NativeTypeName("uint32_t")]
    public uint first_mb_in_slice;

    public StdVideoH264SliceType slice_type;

    [NativeTypeName("int8_t")]
    public sbyte slice_alpha_c0_offset_div2;

    [NativeTypeName("int8_t")]
    public sbyte slice_beta_offset_div2;

    [NativeTypeName("int8_t")]
    public sbyte slice_qp_delta;

    [NativeTypeName("uint8_t")]
    public byte reserved1;

    public StdVideoH264CabacInitIdc cabac_init_idc;

    public StdVideoH264DisableDeblockingFilterIdc disable_deblocking_filter_idc;

    [NativeTypeName("const StdVideoEncodeH264WeightTable *")]
    public StdVideoEncodeH264WeightTable* pWeightTable;
}
