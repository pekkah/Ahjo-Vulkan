using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public partial struct StdVideoH265PredictorPaletteEntries
{
    [NativeTypeName("uint16_t[3][128]")]
    public _PredictorPaletteEntries_e__FixedBuffer PredictorPaletteEntries;

    [InlineArray(3 * 128)]
    public partial struct _PredictorPaletteEntries_e__FixedBuffer
    {
        public ushort e0_0;
    }
}
