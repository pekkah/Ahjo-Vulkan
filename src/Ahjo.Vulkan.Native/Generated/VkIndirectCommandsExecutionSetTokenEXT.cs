namespace Ahjo.Vulkan.Native;

public partial struct VkIndirectCommandsExecutionSetTokenEXT
{
    public VkIndirectExecutionSetInfoTypeEXT type;

    [NativeTypeName("VkShaderStageFlags")]
    public uint shaderStages;
}
