using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct StdVideoAV1TileInfo
{
    public StdVideoAV1TileInfoFlags flags;

    [NativeTypeName("uint8_t")]
    public byte TileCols;

    [NativeTypeName("uint8_t")]
    public byte TileRows;

    [NativeTypeName("uint16_t")]
    public ushort context_update_tile_id;

    [NativeTypeName("uint8_t")]
    public byte tile_size_bytes_minus_1;

    [NativeTypeName("uint8_t[7]")]
    public _reserved1_e__FixedBuffer reserved1;

    [NativeTypeName("const uint16_t *")]
    public ushort* pMiColStarts;

    [NativeTypeName("const uint16_t *")]
    public ushort* pMiRowStarts;

    [NativeTypeName("const uint16_t *")]
    public ushort* pWidthInSbsMinus1;

    [NativeTypeName("const uint16_t *")]
    public ushort* pHeightInSbsMinus1;

    [InlineArray(7)]
    public partial struct _reserved1_e__FixedBuffer
    {
        public byte e0;
    }
}
