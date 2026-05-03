namespace Ahjo.Vulkan.Native;

public unsafe partial struct StdVideoH264PictureParameterSet
{
    public StdVideoH264PpsFlags flags;

    [NativeTypeName("uint8_t")]
    public byte seq_parameter_set_id;

    [NativeTypeName("uint8_t")]
    public byte pic_parameter_set_id;

    [NativeTypeName("uint8_t")]
    public byte num_ref_idx_l0_default_active_minus1;

    [NativeTypeName("uint8_t")]
    public byte num_ref_idx_l1_default_active_minus1;

    public StdVideoH264WeightedBipredIdc weighted_bipred_idc;

    [NativeTypeName("int8_t")]
    public sbyte pic_init_qp_minus26;

    [NativeTypeName("int8_t")]
    public sbyte pic_init_qs_minus26;

    [NativeTypeName("int8_t")]
    public sbyte chroma_qp_index_offset;

    [NativeTypeName("int8_t")]
    public sbyte second_chroma_qp_index_offset;

    [NativeTypeName("const StdVideoH264ScalingLists *")]
    public StdVideoH264ScalingLists* pScalingLists;
}
