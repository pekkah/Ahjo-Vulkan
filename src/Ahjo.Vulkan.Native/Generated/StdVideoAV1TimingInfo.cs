namespace Ahjo.Vulkan.Native;

public partial struct StdVideoAV1TimingInfo
{
    public StdVideoAV1TimingInfoFlags flags;

    [NativeTypeName("uint32_t")]
    public uint num_units_in_display_tick;

    [NativeTypeName("uint32_t")]
    public uint time_scale;

    [NativeTypeName("uint32_t")]
    public uint num_ticks_per_picture_minus_1;
}
