using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoDecodeAV1ReferenceInfo
{
    public StdVideoDecodeAV1ReferenceInfoFlags flags;

    [NativeTypeName("uint8_t")]
    public byte frame_type;

    [NativeTypeName("uint8_t")]
    public byte RefFrameSignBias;

    [NativeTypeName("uint8_t")]
    public byte OrderHint;

    [NativeTypeName("uint8_t[8]")]
    public _SavedOrderHints_e__FixedBuffer SavedOrderHints;

    [InlineArray(8)]
    public partial struct _SavedOrderHints_e__FixedBuffer
    {
        public byte e0;
    }
}
