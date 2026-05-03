namespace Ahjo.Vulkan.Native;

public partial struct VkVideoEncodeAV1QIndexKHR
{
    [NativeTypeName("uint32_t")]
    public uint intraQIndex;

    [NativeTypeName("uint32_t")]
    public uint predictiveQIndex;

    [NativeTypeName("uint32_t")]
    public uint bipredictiveQIndex;
}
