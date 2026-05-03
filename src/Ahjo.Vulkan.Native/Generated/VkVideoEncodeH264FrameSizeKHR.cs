namespace Ahjo.Vulkan.Native;

public partial struct VkVideoEncodeH264FrameSizeKHR
{
    [NativeTypeName("uint32_t")]
    public uint frameISize;

    [NativeTypeName("uint32_t")]
    public uint framePSize;

    [NativeTypeName("uint32_t")]
    public uint frameBSize;
}
