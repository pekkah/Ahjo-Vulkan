namespace Ahjo.Vulkan.Native;

public partial struct VkDrmFormatModifierProperties2EXT
{
    [NativeTypeName("uint64_t")]
    public ulong drmFormatModifier;

    [NativeTypeName("uint32_t")]
    public uint drmFormatModifierPlaneCount;

    [NativeTypeName("VkFormatFeatureFlags2")]
    public ulong drmFormatModifierTilingFeatures;
}
