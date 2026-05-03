namespace Ahjo.Vulkan.Native;

public partial struct VkExtent2D
{
    [NativeTypeName("uint32_t")]
    public uint width;

    [NativeTypeName("uint32_t")]
    public uint height;
}
