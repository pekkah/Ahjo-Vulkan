namespace Ahjo.Vulkan.Native;

public partial struct StdVideoEncodeAV1ExtensionHeader
{
    [NativeTypeName("uint8_t")]
    public byte temporal_id;

    [NativeTypeName("uint8_t")]
    public byte spatial_id;
}
