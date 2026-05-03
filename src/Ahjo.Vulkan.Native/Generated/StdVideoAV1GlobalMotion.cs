using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoAV1GlobalMotion
{
    [NativeTypeName("uint8_t[8]")]
    public _GmType_e__FixedBuffer GmType;

    [NativeTypeName("int32_t[8][6]")]
    public _gm_params_e__FixedBuffer gm_params;

    [InlineArray(8)]
    public partial struct _GmType_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8 * 6)]
    public partial struct _gm_params_e__FixedBuffer
    {
        public int e0_0;
    }
}
