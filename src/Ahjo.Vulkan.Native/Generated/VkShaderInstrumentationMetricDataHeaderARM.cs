namespace Ahjo.Vulkan.Native;

public partial struct VkShaderInstrumentationMetricDataHeaderARM
{
    [NativeTypeName("uint32_t")]
    public uint resultIndex;

    [NativeTypeName("uint32_t")]
    public uint resultSubIndex;

    [NativeTypeName("VkShaderStageFlags")]
    public uint stages;

    [NativeTypeName("uint32_t")]
    public uint basicBlockIndex;
}
