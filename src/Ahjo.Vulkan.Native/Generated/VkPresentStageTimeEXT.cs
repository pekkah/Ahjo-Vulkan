namespace Ahjo.Vulkan.Native;

public partial struct VkPresentStageTimeEXT
{
    [NativeTypeName("VkPresentStageFlagsEXT")]
    public uint stage;

    [NativeTypeName("uint64_t")]
    public ulong time;
}
