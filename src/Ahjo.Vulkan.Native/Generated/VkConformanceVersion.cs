namespace Ahjo.Vulkan.Native;

public partial struct VkConformanceVersion
{
    [NativeTypeName("uint8_t")]
    public byte major;

    [NativeTypeName("uint8_t")]
    public byte minor;

    [NativeTypeName("uint8_t")]
    public byte subminor;

    [NativeTypeName("uint8_t")]
    public byte patch;
}
