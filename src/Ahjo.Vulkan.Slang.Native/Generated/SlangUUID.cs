using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Slang.Native;

public partial struct SlangUUID
{
    [NativeTypeName("uint32_t")]
    public uint data1;

    [NativeTypeName("uint16_t")]
    public ushort data2;

    [NativeTypeName("uint16_t")]
    public ushort data3;

    [NativeTypeName("uint8_t[8]")]
    public _data4_e__FixedBuffer data4;

    [InlineArray(8)]
    public partial struct _data4_e__FixedBuffer
    {
        public byte e0;
    }
}
