namespace Ahjo.Vulkan.Native;

public partial struct StdVideoAV1ColorConfig
{
    public StdVideoAV1ColorConfigFlags flags;

    [NativeTypeName("uint8_t")]
    public byte BitDepth;

    [NativeTypeName("uint8_t")]
    public byte subsampling_x;

    [NativeTypeName("uint8_t")]
    public byte subsampling_y;

    [NativeTypeName("uint8_t")]
    public byte reserved1;

    public StdVideoAV1ColorPrimaries color_primaries;

    public StdVideoAV1TransferCharacteristics transfer_characteristics;

    public StdVideoAV1MatrixCoefficients matrix_coefficients;

    public StdVideoAV1ChromaSamplePosition chroma_sample_position;
}
