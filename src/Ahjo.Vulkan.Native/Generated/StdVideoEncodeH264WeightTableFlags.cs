namespace Ahjo.Vulkan.Native;

public partial struct StdVideoEncodeH264WeightTableFlags
{
    [NativeTypeName("uint32_t")]
    public uint luma_weight_l0_flag;

    [NativeTypeName("uint32_t")]
    public uint chroma_weight_l0_flag;

    [NativeTypeName("uint32_t")]
    public uint luma_weight_l1_flag;

    [NativeTypeName("uint32_t")]
    public uint chroma_weight_l1_flag;
}
