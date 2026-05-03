namespace Ahjo.Vulkan.Native;

public partial struct VkMicromapTriangleEXT
{
    [NativeTypeName("uint32_t")]
    public uint dataOffset;

    [NativeTypeName("uint16_t")]
    public ushort subdivisionLevel;

    [NativeTypeName("uint16_t")]
    public ushort format;
}
