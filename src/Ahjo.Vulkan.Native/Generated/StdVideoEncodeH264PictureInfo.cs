using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct StdVideoEncodeH264PictureInfo
{
    public StdVideoEncodeH264PictureInfoFlags flags;

    [NativeTypeName("uint8_t")]
    public byte seq_parameter_set_id;

    [NativeTypeName("uint8_t")]
    public byte pic_parameter_set_id;

    [NativeTypeName("uint16_t")]
    public ushort idr_pic_id;

    public StdVideoH264PictureType primary_pic_type;

    [NativeTypeName("uint32_t")]
    public uint frame_num;

    [NativeTypeName("int32_t")]
    public int PicOrderCnt;

    [NativeTypeName("uint8_t")]
    public byte temporal_id;

    [NativeTypeName("uint8_t[3]")]
    public _reserved1_e__FixedBuffer reserved1;

    [NativeTypeName("const StdVideoEncodeH264ReferenceListsInfo *")]
    public StdVideoEncodeH264ReferenceListsInfo* pRefLists;

    [InlineArray(3)]
    public partial struct _reserved1_e__FixedBuffer
    {
        public byte e0;
    }
}
