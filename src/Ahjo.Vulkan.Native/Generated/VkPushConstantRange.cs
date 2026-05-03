namespace Ahjo.Vulkan.Native;

public partial struct VkPushConstantRange
{
    [NativeTypeName("VkShaderStageFlags")]
    public uint stageFlags;

    [NativeTypeName("uint32_t")]
    public uint offset;

    [NativeTypeName("uint32_t")]
    public uint size;
}
