namespace Ahjo.Vulkan.Native;

public partial struct StdVideoEncodeH265ReferenceInfo
{
    public StdVideoEncodeH265ReferenceInfoFlags flags;

    public StdVideoH265PictureType pic_type;

    [NativeTypeName("int32_t")]
    public int PicOrderCntVal;

    [NativeTypeName("uint8_t")]
    public byte TemporalId;
}
