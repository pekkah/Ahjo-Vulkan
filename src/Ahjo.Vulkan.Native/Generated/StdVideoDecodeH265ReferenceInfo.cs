namespace Ahjo.Vulkan.Native;

public partial struct StdVideoDecodeH265ReferenceInfo
{
    public StdVideoDecodeH265ReferenceInfoFlags flags;

    [NativeTypeName("int32_t")]
    public int PicOrderCntVal;
}
