namespace Ahjo.Vulkan.Native;

public partial struct StdVideoAV1Quantization
{
    public StdVideoAV1QuantizationFlags flags;

    [NativeTypeName("uint8_t")]
    public byte base_q_idx;

    [NativeTypeName("int8_t")]
    public sbyte DeltaQYDc;

    [NativeTypeName("int8_t")]
    public sbyte DeltaQUDc;

    [NativeTypeName("int8_t")]
    public sbyte DeltaQUAc;

    [NativeTypeName("int8_t")]
    public sbyte DeltaQVDc;

    [NativeTypeName("int8_t")]
    public sbyte DeltaQVAc;

    [NativeTypeName("uint8_t")]
    public byte qm_y;

    [NativeTypeName("uint8_t")]
    public byte qm_u;

    [NativeTypeName("uint8_t")]
    public byte qm_v;
}
