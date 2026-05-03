namespace Ahjo.Vulkan.Native;

public partial struct StdVideoEncodeH264ReferenceInfo
{
    public StdVideoEncodeH264ReferenceInfoFlags flags;

    public StdVideoH264PictureType primary_pic_type;

    [NativeTypeName("uint32_t")]
    public uint FrameNum;

    [NativeTypeName("int32_t")]
    public int PicOrderCnt;

    [NativeTypeName("uint16_t")]
    public ushort long_term_pic_num;

    [NativeTypeName("uint16_t")]
    public ushort long_term_frame_idx;

    [NativeTypeName("uint8_t")]
    public byte temporal_id;
}
