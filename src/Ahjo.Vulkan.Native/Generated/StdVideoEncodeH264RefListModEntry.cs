namespace Ahjo.Vulkan.Native;

public partial struct StdVideoEncodeH264RefListModEntry
{
    public StdVideoH264ModificationOfPicNumsIdc modification_of_pic_nums_idc;

    [NativeTypeName("uint16_t")]
    public ushort abs_diff_pic_num_minus1;

    [NativeTypeName("uint16_t")]
    public ushort long_term_pic_num;
}
