namespace Ahjo.Vulkan.Native;

public partial struct StdVideoVP9ColorConfig
{
    public StdVideoVP9ColorConfigFlags flags;

    [NativeTypeName("uint8_t")]
    public byte BitDepth;

    [NativeTypeName("uint8_t")]
    public byte subsampling_x;

    [NativeTypeName("uint8_t")]
    public byte subsampling_y;

    [NativeTypeName("uint8_t")]
    public byte reserved1;

    public StdVideoVP9ColorSpace color_space;
}
