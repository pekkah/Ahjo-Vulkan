namespace Ahjo.Vulkan.Native;

public partial struct VkGpaPerfBlockPropertiesAMD
{
    public VkGpaPerfBlockAMD blockType;

    [NativeTypeName("VkGpaPerfBlockPropertiesFlagsAMD")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint instanceCount;

    [NativeTypeName("uint32_t")]
    public uint maxEventID;

    [NativeTypeName("uint32_t")]
    public uint maxGlobalOnlyCounters;

    [NativeTypeName("uint32_t")]
    public uint maxGlobalSharedCounters;

    [NativeTypeName("uint32_t")]
    public uint maxStreamingCounters;
}
