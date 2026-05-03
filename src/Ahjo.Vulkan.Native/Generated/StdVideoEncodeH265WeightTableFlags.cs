namespace Ahjo.Vulkan.Native;

public partial struct StdVideoEncodeH265WeightTableFlags
{
    [NativeTypeName("uint16_t")]
    public ushort luma_weight_l0_flag;

    [NativeTypeName("uint16_t")]
    public ushort chroma_weight_l0_flag;

    [NativeTypeName("uint16_t")]
    public ushort luma_weight_l1_flag;

    [NativeTypeName("uint16_t")]
    public ushort chroma_weight_l1_flag;
}
