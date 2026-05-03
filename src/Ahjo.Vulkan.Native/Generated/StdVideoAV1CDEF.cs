using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoAV1CDEF
{
    [NativeTypeName("uint8_t")]
    public byte cdef_damping_minus_3;

    [NativeTypeName("uint8_t")]
    public byte cdef_bits;

    [NativeTypeName("uint8_t[8]")]
    public _cdef_y_pri_strength_e__FixedBuffer cdef_y_pri_strength;

    [NativeTypeName("uint8_t[8]")]
    public _cdef_y_sec_strength_e__FixedBuffer cdef_y_sec_strength;

    [NativeTypeName("uint8_t[8]")]
    public _cdef_uv_pri_strength_e__FixedBuffer cdef_uv_pri_strength;

    [NativeTypeName("uint8_t[8]")]
    public _cdef_uv_sec_strength_e__FixedBuffer cdef_uv_sec_strength;

    [InlineArray(8)]
    public partial struct _cdef_y_pri_strength_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8)]
    public partial struct _cdef_y_sec_strength_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8)]
    public partial struct _cdef_uv_pri_strength_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8)]
    public partial struct _cdef_uv_sec_strength_e__FixedBuffer
    {
        public byte e0;
    }
}
