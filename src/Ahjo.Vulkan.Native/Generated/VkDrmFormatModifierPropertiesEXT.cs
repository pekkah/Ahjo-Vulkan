namespace Ahjo.Vulkan.Native;

public partial struct VkDrmFormatModifierPropertiesEXT
{
    [NativeTypeName("uint64_t")]
    public ulong drmFormatModifier;

    [NativeTypeName("uint32_t")]
    public uint drmFormatModifierPlaneCount;

    [NativeTypeName("VkFormatFeatureFlags")]
    public uint drmFormatModifierTilingFeatures;
}
