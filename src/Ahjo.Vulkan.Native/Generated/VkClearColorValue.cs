using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

[StructLayout(LayoutKind.Explicit)]
public partial struct VkClearColorValue
{
    [FieldOffset(0)]
    [NativeTypeName("float[4]")]
    public _float32_e__FixedBuffer float32;

    [FieldOffset(0)]
    [NativeTypeName("int32_t[4]")]
    public _int32_e__FixedBuffer int32;

    [FieldOffset(0)]
    [NativeTypeName("uint32_t[4]")]
    public _uint32_e__FixedBuffer uint32;

    [InlineArray(4)]
    public partial struct _float32_e__FixedBuffer
    {
        public float e0;
    }

    [InlineArray(4)]
    public partial struct _int32_e__FixedBuffer
    {
        public int e0;
    }

    [InlineArray(4)]
    public partial struct _uint32_e__FixedBuffer
    {
        public uint e0;
    }
}
