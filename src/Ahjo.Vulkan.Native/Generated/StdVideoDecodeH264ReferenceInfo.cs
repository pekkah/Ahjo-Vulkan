using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoDecodeH264ReferenceInfo
{
    public StdVideoDecodeH264ReferenceInfoFlags flags;

    [NativeTypeName("uint16_t")]
    public ushort FrameNum;

    [NativeTypeName("uint16_t")]
    public ushort reserved;

    [NativeTypeName("int32_t[2]")]
    public _PicOrderCnt_e__FixedBuffer PicOrderCnt;

    [InlineArray(2)]
    public partial struct _PicOrderCnt_e__FixedBuffer
    {
        public int e0;
    }
}
