namespace Ahjo.Vulkan.Native;

public partial struct VkDispatchIndirectCommand
{
    [NativeTypeName("uint32_t")]
    public uint x;

    [NativeTypeName("uint32_t")]
    public uint y;

    [NativeTypeName("uint32_t")]
    public uint z;
}
