namespace Ahjo.Vulkan.Native;

public partial struct VkMultiDrawInfoEXT
{
    [NativeTypeName("uint32_t")]
    public uint firstVertex;

    [NativeTypeName("uint32_t")]
    public uint vertexCount;
}
