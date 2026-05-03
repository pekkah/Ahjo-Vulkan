using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoH265LongTermRefPicsSps
{
    [NativeTypeName("uint32_t")]
    public uint used_by_curr_pic_lt_sps_flag;

    [NativeTypeName("uint32_t[32]")]
    public _lt_ref_pic_poc_lsb_sps_e__FixedBuffer lt_ref_pic_poc_lsb_sps;

    [InlineArray(32)]
    public partial struct _lt_ref_pic_poc_lsb_sps_e__FixedBuffer
    {
        public uint e0;
    }
}
