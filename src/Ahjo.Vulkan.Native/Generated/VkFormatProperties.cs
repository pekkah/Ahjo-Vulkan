namespace Ahjo.Vulkan.Native;

public partial struct VkFormatProperties
{
    [NativeTypeName("VkFormatFeatureFlags")]
    public uint linearTilingFeatures;

    [NativeTypeName("VkFormatFeatureFlags")]
    public uint optimalTilingFeatures;

    [NativeTypeName("VkFormatFeatureFlags")]
    public uint bufferFeatures;
}
