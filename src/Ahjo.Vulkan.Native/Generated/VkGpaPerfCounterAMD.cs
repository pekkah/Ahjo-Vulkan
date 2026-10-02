namespace Ahjo.Vulkan.Native;

public partial struct VkGpaPerfCounterAMD
{
    public VkGpaPerfBlockAMD blockType;

    [NativeTypeName("uint32_t")]
    public uint blockInstance;

    [NativeTypeName("uint32_t")]
    public uint eventID;
}
