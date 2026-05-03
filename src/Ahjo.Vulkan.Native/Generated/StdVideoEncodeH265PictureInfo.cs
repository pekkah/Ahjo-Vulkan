using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct StdVideoEncodeH265PictureInfo
{
    public StdVideoEncodeH265PictureInfoFlags flags;

    public StdVideoH265PictureType pic_type;

    [NativeTypeName("uint8_t")]
    public byte sps_video_parameter_set_id;

    [NativeTypeName("uint8_t")]
    public byte pps_seq_parameter_set_id;

    [NativeTypeName("uint8_t")]
    public byte pps_pic_parameter_set_id;

    [NativeTypeName("uint8_t")]
    public byte short_term_ref_pic_set_idx;

    [NativeTypeName("int32_t")]
    public int PicOrderCntVal;

    [NativeTypeName("uint8_t")]
    public byte TemporalId;

    [NativeTypeName("uint8_t[7]")]
    public _reserved1_e__FixedBuffer reserved1;

    [NativeTypeName("const StdVideoEncodeH265ReferenceListsInfo *")]
    public StdVideoEncodeH265ReferenceListsInfo* pRefLists;

    [NativeTypeName("const StdVideoH265ShortTermRefPicSet *")]
    public StdVideoH265ShortTermRefPicSet* pShortTermRefPicSet;

    [NativeTypeName("const StdVideoEncodeH265LongTermRefPics *")]
    public StdVideoEncodeH265LongTermRefPics* pLongTermRefPics;

    [InlineArray(7)]
    public partial struct _reserved1_e__FixedBuffer
    {
        public byte e0;
    }
}
