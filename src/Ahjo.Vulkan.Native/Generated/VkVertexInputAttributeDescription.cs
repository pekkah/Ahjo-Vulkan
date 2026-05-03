namespace Ahjo.Vulkan.Native;

public partial struct VkVertexInputAttributeDescription
{
    [NativeTypeName("uint32_t")]
    public uint location;

    [NativeTypeName("uint32_t")]
    public uint binding;

    public VkFormat format;

    [NativeTypeName("uint32_t")]
    public uint offset;
}
