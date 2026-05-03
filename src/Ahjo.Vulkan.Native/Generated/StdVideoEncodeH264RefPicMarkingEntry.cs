namespace Ahjo.Vulkan.Native;

public partial struct StdVideoEncodeH264RefPicMarkingEntry
{
    public StdVideoH264MemMgmtControlOp memory_management_control_operation;

    [NativeTypeName("uint16_t")]
    public ushort difference_of_pic_nums_minus1;

    [NativeTypeName("uint16_t")]
    public ushort long_term_pic_num;

    [NativeTypeName("uint16_t")]
    public ushort long_term_frame_idx;

    [NativeTypeName("uint16_t")]
    public ushort max_long_term_frame_idx_plus1;
}
