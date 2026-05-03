namespace Ahjo.Vulkan.Native;

public partial struct VkExtent3D
{
    [NativeTypeName("uint32_t")]
    public uint width;

    [NativeTypeName("uint32_t")]
    public uint height;

    [NativeTypeName("uint32_t")]
    public uint depth;
}
