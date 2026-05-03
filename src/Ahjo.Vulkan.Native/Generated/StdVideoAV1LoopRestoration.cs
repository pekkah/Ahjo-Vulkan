using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoAV1LoopRestoration
{
    [NativeTypeName("StdVideoAV1FrameRestorationType[3]")]
    public _FrameRestorationType_e__FixedBuffer FrameRestorationType;

    [NativeTypeName("uint16_t[3]")]
    public _LoopRestorationSize_e__FixedBuffer LoopRestorationSize;

    [InlineArray(3)]
    public partial struct _FrameRestorationType_e__FixedBuffer
    {
        public StdVideoAV1FrameRestorationType e0;
    }

    [InlineArray(3)]
    public partial struct _LoopRestorationSize_e__FixedBuffer
    {
        public ushort e0;
    }
}
