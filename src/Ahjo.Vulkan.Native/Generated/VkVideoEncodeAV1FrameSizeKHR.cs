namespace Ahjo.Vulkan.Native;

public partial struct VkVideoEncodeAV1FrameSizeKHR
{
    [NativeTypeName("uint32_t")]
    public uint intraFrameSize;

    [NativeTypeName("uint32_t")]
    public uint predictiveFrameSize;

    [NativeTypeName("uint32_t")]
    public uint bipredictiveFrameSize;
}
