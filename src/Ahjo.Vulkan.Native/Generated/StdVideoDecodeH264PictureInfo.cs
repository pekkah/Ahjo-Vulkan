using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoDecodeH264PictureInfo
{
    public StdVideoDecodeH264PictureInfoFlags flags;

    [NativeTypeName("uint8_t")]
    public byte seq_parameter_set_id;

    [NativeTypeName("uint8_t")]
    public byte pic_parameter_set_id;

    [NativeTypeName("uint8_t")]
    public byte reserved1;

    [NativeTypeName("uint8_t")]
    public byte reserved2;

    [NativeTypeName("uint16_t")]
    public ushort frame_num;

    [NativeTypeName("uint16_t")]
    public ushort idr_pic_id;

    [NativeTypeName("int32_t[2]")]
    public _PicOrderCnt_e__FixedBuffer PicOrderCnt;

    [InlineArray(2)]
    public partial struct _PicOrderCnt_e__FixedBuffer
    {
        public int e0;
    }
}
